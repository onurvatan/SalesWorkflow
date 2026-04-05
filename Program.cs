using SalesWorkflow.Agents;
using SalesWorkflow.Data;
using SalesWorkflow.Ecommerce;
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
    var indexService = scope.ServiceProvider.GetService<EcommerceIndexService>();
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

// ─── Pattern 4: E-Commerce Sales Agent ─────────────────────────────────────
// SalesAgent uses two tools in a single turn: catalog_search (vector) + stock_check (in-memory).
// The LLM decides whether to call them in parallel or sequence based on the user's question.
app.MapPost("/agents/sales",
    async (ChatRequest req, [FromKeyedServices(SalesAgent.AgentName)] AIAgent agent) =>
    {
        var result = await agent.RunAsync(
            [new ChatMessage(ChatRole.User, req.Input)], null, null, default);
        return Results.Ok(new { agentName = SalesAgent.AgentName, result = result.Text });
    })
    .WithName("RunSalesAgent")
    .WithSummary("Single agent — SalesAgent with catalog_search and stock_check tools");

// ─── Pattern 5: Sales Workflow Agent ────────────────────────────────────────
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

app.Run();

public record ChatRequest(string Input);
