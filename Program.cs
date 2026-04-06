using SalesWorkflow.Data;
using SalesWorkflow.Services;
using SalesWorkflow.Extensions;
using SalesWorkflow.Infrastructure;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.DevUI;
using Microsoft.Extensions.AI;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.AddSalesWorkflowApp();
builder.AddDevUI();
builder.AddOpenAIResponses();
builder.AddOpenAIConversations();
builder.Services.AddOpenApi(options =>
{
    // Declare X-Api-Key as an API key security scheme so Swagger UI shows the Authorize button
    options.AddDocumentTransformer((doc, _, _) =>
    {
        doc.Components ??= new OpenApiComponents();
        doc.Components.SecuritySchemes!["ApiKey"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = "X-Api-Key",
            Description = "Required for /admin/* endpoints. Enter your back-office API key."
        };
        return Task.CompletedTask;
    });

    options.AddOperationTransformer((operation, context, _) =>
    {
        // Apply the security requirement only to /admin/* routes
        if (context.Description.RelativePath?.StartsWith("admin/", StringComparison.OrdinalIgnoreCase) == true)
        {
            operation.Security =
            [
                new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("ApiKey")] = []
                }
            ];
        }
        return Task.CompletedTask;
    });
});
builder.Services.AddSwaggerUI();

var app = builder.Build();

// Index the electronics catalog into Azure AI Search on startup (idempotent — safe to restart)
app.Lifetime.ApplicationStarted.Register(() =>
{
    using var scope = app.Services.CreateScope();
    var indexService = scope.ServiceProvider.GetService<CatalogIndexService>();
    if (indexService is null)
    {
        app.Logger.LogInformation("Catalog indexing skipped — set SalesIndex:CatalogIndexName to enable.");
        return;
    }
    try
    {
        var repo = scope.ServiceProvider.GetRequiredService<IProductRepository>();
        indexService.EnsureIndexExistsAsync(CancellationToken.None).GetAwaiter().GetResult();
        indexService.IndexProductsAsync(repo.GetAll(), CancellationToken.None).GetAwaiter().GetResult();
        app.Logger.LogInformation("E-commerce catalog ready: {Count} products indexed.", repo.GetAll().Count);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Catalog indexing failed — SalesAgent may not work correctly.");
    }
});

// Intercept unhandled exceptions before the framework's own serializer attempts
// to serialize the Exception object (which fails on Exception.TargetSite: MethodBase).
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async ctx =>
    {
        var feature = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var ex = feature?.Error;
        app.Logger.LogError(ex, "Unhandled exception on {Method} {Path}",
            ctx.Request.Method, ctx.Request.Path);
        ctx.Response.StatusCode = 500;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(new
        {
            error = ex?.GetType().Name ?? "UnknownError",
            message = ex?.Message ?? "An unexpected error occurred"
        });
    });
});

if (app.Environment.IsDevelopment())
{
    app.MapDevUI();
    app.MapOpenApi();
    app.MapSwaggerUI();
}

// Health checks — includes catalog index connectivity probe when SalesIndex is configured
app.MapHealthChecks("/health");

// OpenAI Responses API — used by the Agent Framework DevUI
app.MapOpenAIResponses();
app.MapOpenAIConversations();


// ─── Sales Workflow Agent ────────────────────────────────────────
// SalesWorkflowAgent is a 3-step sequential workflow:
//   catalog-retriever → stock-checker → sales-responder
app.MapPost("/agents/sales-workflow",
    async (ChatRequest req,
           [FromKeyedServices("SalesWorkflowAgent")] AIAgent agent,
           IConversationHistoryStore store,
           ILogger<Program> logger) =>
    {
        var sessionId = req.SessionId ?? Guid.NewGuid().ToString("N");
        logger.LogInformation("[SalesWorkflowAgent] Session={SessionId} Received: {Input}", sessionId, req.Input);
        try
        {
            var history = store.GetOrCreate(sessionId);
            history.Add(new ChatMessage(ChatRole.User, req.Input));
            var result = await agent.RunAsync(history, null, null, default);
            history.Add(new ChatMessage(ChatRole.Assistant, result.Text ?? string.Empty));
            store.Save(sessionId, history);
            logger.LogInformation("[SalesWorkflowAgent] Session={SessionId} Completed.", sessionId);
            return Results.Ok(new { agentName = "SalesWorkflowAgent", sessionId, result = result.Text });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[SalesWorkflowAgent] Session={SessionId} Execution failed.", sessionId);
            return Results.Problem(ex.Message, statusCode: 500);
        }
    })
    .WithName("RunSalesWorkflow")
    .WithSummary("Workflow agent — catalog-retriever → stock-checker → sales-responder (sequential)");

// ─── Customer Service Workflow Agent ────────────────────────────
// CustomerServiceWorkflowAgent uses the Handoff pattern:
//   triage-agent → billing-specialist | shipping-specialist
// The triage agent classifies the customer's issue and routes them to
// the appropriate specialist. Billing specialist can escalate to human.
app.MapPost("/agents/customer-service",
    async (ChatRequest req,
           [FromKeyedServices("CustomerServiceWorkflowAgent")] AIAgent agent,
           IConversationHistoryStore store,
           ILogger<Program> logger) =>
    {
        var sessionId = req.SessionId ?? Guid.NewGuid().ToString("N");
        logger.LogInformation("[CustomerServiceWorkflowAgent] Session={SessionId} Received: {Input}", sessionId, req.Input);
        try
        {
            var history = store.GetOrCreate(sessionId);
            history.Add(new ChatMessage(ChatRole.User, req.Input));
            var result = await agent.RunAsync(history, null, null, default);
            history.Add(new ChatMessage(ChatRole.Assistant, result.Text ?? string.Empty));
            store.Save(sessionId, history);
            logger.LogInformation("[CustomerServiceWorkflowAgent] Session={SessionId} Completed.", sessionId);
            return Results.Ok(new { agentName = "CustomerServiceWorkflowAgent", sessionId, result = result.Text });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[CustomerServiceWorkflowAgent] Session={SessionId} Execution failed.", sessionId);
            return Results.Problem(ex.Message, statusCode: 500);
        }
    })
    .WithName("RunCustomerService")
    .WithSummary("Handoff workflow — triage-agent → billing-specialist | shipping-specialist");

// ─── After-Sale Report Workflow Agent ───────────────────────────
// AfterSaleReportWorkflowAgent uses the Concurrent pattern:
//   sales-analyst ‖ satisfaction-analyst → merged admin report
// Both analysts execute in parallel; the aggregator merges their
// outputs into a single report for admin consumption.
app.MapPost("/agents/after-sale-report",
    async (ChatRequest req,
           [FromKeyedServices("AfterSaleReportWorkflowAgent")] AIAgent agent,
           IConversationHistoryStore store,
           ILogger<Program> logger) =>
    {
        var sessionId = req.SessionId ?? Guid.NewGuid().ToString("N");
        logger.LogInformation("[AfterSaleReportWorkflowAgent] Session={SessionId} Received: {Input}", sessionId, req.Input);
        try
        {
            var history = store.GetOrCreate(sessionId);
            history.Add(new ChatMessage(ChatRole.User, req.Input));
            var result = await agent.RunAsync(history, null, null, default);
            history.Add(new ChatMessage(ChatRole.Assistant, result.Text ?? string.Empty));
            store.Save(sessionId, history);
            logger.LogInformation("[AfterSaleReportWorkflowAgent] Session={SessionId} Completed.", sessionId);
            return Results.Ok(new { agentName = "AfterSaleReportWorkflowAgent", sessionId, result = result.Text });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[AfterSaleReportWorkflowAgent] Session={SessionId} Execution failed.", sessionId);
            return Results.Problem(ex.Message, statusCode: 500);
        }
    })
    .WithName("RunAfterSaleReport")
    .WithSummary("Concurrent workflow — sales-analyst ‖ satisfaction-analyst → merged admin report");

// ─── Orchestrator ────────────────────────────────────────────────
// OrchestratorAgent is a GroupChat workflow with LLM-driven routing:
//   OrchestratorGroupChatManager classifies intent → selects participant:
//     CustomerServiceWorkflowAgent | SalesWorkflowAgent
app.MapPost("/agents",
    async (ChatRequest req,
           [FromKeyedServices("OrchestratorAgent")] AIAgent agent,
           IConversationHistoryStore store,
           ILogger<Program> logger) =>
    {
        var sessionId = req.SessionId ?? Guid.NewGuid().ToString("N");
        logger.LogInformation("[OrchestratorAgent] Session={SessionId} Received: {Input}", sessionId, req.Input);
        try
        {
            var history = store.GetOrCreate(sessionId);
            history.Add(new ChatMessage(ChatRole.User, req.Input));
            var result = await agent.RunAsync(history, null, null, default);
            history.Add(new ChatMessage(ChatRole.Assistant, result.Text ?? string.Empty));
            store.Save(sessionId, history);
            logger.LogInformation("[OrchestratorAgent] Session={SessionId} Completed.", sessionId);
            return Results.Ok(new { agentName = "OrchestratorAgent", sessionId, result = result.Text });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[OrchestratorAgent] Session={SessionId} Execution failed.", sessionId);
            return Results.Problem(ex.Message, statusCode: 500);
        }
    })
    .WithName("RunOrchestrator")
    .WithSummary("GroupChat orchestrator — routes customer prompt to CustomerService | SalesWorkflow");

// ─── Back-Office (admin, API-key protected) ─────────────────────
// BackOfficeOrchestratorAgent routes admin prompts to AfterSaleReportWorkflowAgent.
// All /admin/* routes require the X-Api-Key header matching BackOffice:ApiKey in config.
var adminGroup = app.MapGroup("/admin")
    .AddEndpointFilter<ApiKeyEndpointFilter>();

adminGroup.MapPost("/agents",
    async (ChatRequest req,
           [FromKeyedServices("BackOfficeOrchestratorAgent")] AIAgent agent,
           IConversationHistoryStore store,
           ILogger<Program> logger) =>
    {
        var sessionId = req.SessionId ?? Guid.NewGuid().ToString("N");
        logger.LogInformation("[BackOfficeOrchestratorAgent] Session={SessionId} Received: {Input}", sessionId, req.Input);
        try
        {
            var history = store.GetOrCreate(sessionId);
            history.Add(new ChatMessage(ChatRole.User, req.Input));
            var result = await agent.RunAsync(history, null, null, default);
            history.Add(new ChatMessage(ChatRole.Assistant, result.Text ?? string.Empty));
            store.Save(sessionId, history);
            logger.LogInformation("[BackOfficeOrchestratorAgent] Session={SessionId} Completed.", sessionId);
            return Results.Ok(new { agentName = "BackOfficeOrchestratorAgent", sessionId, result = result.Text });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[BackOfficeOrchestratorAgent] Session={SessionId} Execution failed.", sessionId);
            return Results.Problem(ex.Message, statusCode: 500);
        }
    })
    .WithName("RunBackOfficeOrchestrator")
    .WithSummary("Admin GroupChat orchestrator — routes back-office prompts to AfterSaleReport (requires X-Api-Key)");

// ─── Session management ──────────────────────────────────────────
// DELETE /sessions/{sessionId} — clear conversation history for a session.
// Useful when the user wants to start a fresh conversation without generating
// a new client-side session ID, or for test teardown.
app.MapDelete("/sessions/{sessionId}",
    (string sessionId, IConversationHistoryStore store, ILogger<Program> logger) =>
    {
        var deleted = store.Delete(sessionId);
        if (deleted)
        {
            logger.LogInformation("[Session] Cleared session {SessionId}.", sessionId);
            return Results.NoContent();
        }
        logger.LogWarning("[Session] Delete requested for unknown session {SessionId}.", sessionId);
        return Results.NotFound(new { error = "SessionNotFound", sessionId });
    })
    .WithName("DeleteSession")
    .WithSummary("Clear conversation history for a session. Returns 204 if found, 404 if not.");

app.Run();

// Expose Program to WebApplicationFactory in integration tests
public partial class Program { }
