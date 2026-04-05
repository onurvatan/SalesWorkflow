using SalesWorkflow.Data;
using SalesWorkflow.Services;
using SalesWorkflow.Extensions;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.DevUI;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

builder.AddSalesWorkflowApp();
builder.AddDevUI();
builder.AddOpenAIResponses();
builder.AddOpenAIConversations();
builder.Services.AddOpenApi();
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
    async (ChatRequest req, [FromKeyedServices("SalesWorkflowAgent")] AIAgent agent) =>
    {
        var result = await agent.RunAsync(
            [new ChatMessage(ChatRole.User, req.Input)], null, null, default);
        return Results.Ok(new { agentName = "SalesWorkflowAgent", result = result.Text });
    })
    .WithName("RunSalesWorkflow")
    .WithSummary("Workflow agent — catalog-retriever → stock-checker → sales-responder (sequential)");

// ─── Customer Service Workflow Agent ────────────────────────────
// CustomerServiceWorkflowAgent uses the Handoff pattern:
//   triage-agent → billing-specialist | shipping-specialist
// The triage agent classifies the customer's issue and routes them to
// the appropriate specialist. Billing specialist can escalate to human.
app.MapPost("/agents/customer-service",
    async (ChatRequest req, [FromKeyedServices("CustomerServiceWorkflowAgent")] AIAgent agent) =>
    {
        var result = await agent.RunAsync(
            [new ChatMessage(ChatRole.User, req.Input)], null, null, default);
        return Results.Ok(new { agentName = "CustomerServiceWorkflowAgent", result = result.Text });
    })
    .WithName("RunCustomerService")
    .WithSummary("Handoff workflow — triage-agent → billing-specialist | shipping-specialist");

// ─── After-Sale Report Workflow Agent ───────────────────────────
// AfterSaleReportWorkflowAgent uses the Concurrent pattern:
//   sales-analyst ‖ satisfaction-analyst → merged admin report
// Both analysts execute in parallel; the aggregator merges their
// outputs into a single report for admin consumption.
app.MapPost("/agents/after-sale-report",
    async (ChatRequest req, [FromKeyedServices("AfterSaleReportWorkflowAgent")] AIAgent agent) =>
    {
        var result = await agent.RunAsync(
            [new ChatMessage(ChatRole.User, req.Input)], null, null, default);
        return Results.Ok(new { agentName = "AfterSaleReportWorkflowAgent", result = result.Text });
    })
    .WithName("RunAfterSaleReport")
    .WithSummary("Concurrent workflow — sales-analyst ‖ satisfaction-analyst → merged admin report");

app.Run();

// Expose Program to WebApplicationFactory in integration tests
public partial class Program { }
