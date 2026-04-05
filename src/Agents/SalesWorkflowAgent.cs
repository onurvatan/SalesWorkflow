using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SalesWorkflow.Configuration;
using SalesWorkflow.Data;
using SalesWorkflow.Tools;

namespace SalesWorkflow.Agents;

public static class SalesWorkflowAgent
{
    public const string AgentName = "SalesWorkflowAgent";

    public const string WorkflowDescription =
        "Sequential sales workflow: catalog-retriever → stock-checker → sales-responder.";

    public const string CatalogRetrieverInstructions =
        "Use the catalog_search tool to find products matching the customer's request. Return the full list of matching products with all details.";

    public const string StockCheckerInstructions =
        "For each product mentioned in the previous message, use the stock_check tool to verify its current availability and price. Report the results.";

    public const string SalesResponderInstructions =
        "You are a friendly sales assistant. Using the catalog details and stock information provided, write a helpful recommendation for the customer. For each product include: name, key specs, price, and availability. Flag Low Stock items. Suggest alternatives for Out of Stock products.";

    public static AIAgent Create(IServiceProvider sp, string name)
    {
        var chatClient = sp.GetRequiredService<IChatClient>();
        var azClient = sp.GetRequiredService<AzureOpenAIClient>();
        var settings = sp.GetRequiredService<IOptions<FoundrySettings>>().Value;
        var indexSettings = sp.GetRequiredService<IOptions<SalesIndexSettings>>().Value;
        var catalogClient = sp.GetRequiredKeyedService<SearchClient>("catalog");
        var repo = sp.GetRequiredService<IProductRepository>();

        var catalogRetriever = chatClient.AsAIAgent(
            instructions: CatalogRetrieverInstructions,
            name: "catalog-retriever",
            tools: [CatalogSearchTool.Create(catalogClient, azClient, indexSettings, settings)]);

        var stockChecker = chatClient.AsAIAgent(
            instructions: StockCheckerInstructions,
            name: "stock-checker",
            tools: [StockCheckTool.Create(repo)]);

        var salesResponder = chatClient.AsAIAgent(
            instructions: SalesResponderInstructions,
            name: "sales-responder");

        var workflow = AgentWorkflowBuilder.BuildSequential(name, [catalogRetriever, stockChecker, salesResponder]);
        return Create(workflow, name);
    }

    public static AIAgent Create(Workflow workflow, string name) =>
        workflow.AsAIAgent(
            name,
            name,
            WorkflowDescription,
            InProcessExecution.OffThread,
            includeExceptionDetails: false,
            includeWorkflowOutputsInResponse: false);
}
