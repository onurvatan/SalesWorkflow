using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using SalesWorkflow.Configuration;
using SalesWorkflow.Data;
using SalesWorkflow.Services;
using SalesWorkflow.Infrastructure;
using SalesWorkflow.Tools;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.AzureAI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace SalesWorkflow.Extensions;

public static class ServiceCollectionExtensions
{
    public static WebApplicationBuilder AddSalesWorkflowApp(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<FoundrySettings>()
            .Bind(builder.Configuration.GetSection("Foundry"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services
            .AddOptions<AzureSearchSettings>()
            .Bind(builder.Configuration.GetSection("AzureSearch"));

        builder.Services
            .AddOptions<SalesIndexSettings>()
            .Bind(builder.Configuration.GetSection("SalesIndex"));

        var foundrySettings = builder.Configuration
            .GetSection("Foundry")
            .Get<FoundrySettings>()
            ?? throw new InvalidOperationException("Foundry configuration section is missing.");

        var searchConfig = builder.Configuration
            .GetSection("AzureSearch")
            .Get<AzureSearchSettings>();

        var salesIndexConfig = builder.Configuration
            .GetSection("SalesIndex")
            .Get<SalesIndexSettings>();

        bool catalogConfigured = !string.IsNullOrEmpty(searchConfig?.Endpoint)
                              && !string.IsNullOrEmpty(salesIndexConfig?.CatalogIndexName);

        // Singleton AzureOpenAIClient — use AzureCliCredential in Development for reliability
        var credential = builder.Environment.IsDevelopment()
            ? (Azure.Core.TokenCredential)new AzureCliCredential()
            : new DefaultAzureCredential();

        builder.Services.AddSingleton(
            new AzureOpenAIClient(new Uri(foundrySettings.Endpoint!), credential));

        // IProductRepository is always available — StockCheckTool works without Azure Search
        builder.Services.AddSingleton<IProductRepository, ProductRepository>();

        var hcBuilder = builder.Services.AddHealthChecks();

        if (catalogConfigured)
        {
            // SearchIndexClient — admin plane (index management)
            builder.Services.AddSingleton(
                new SearchIndexClient(new Uri(searchConfig!.Endpoint!), credential));

            // Keyed SearchClient for the catalog index
            builder.Services.AddKeyedSingleton<SearchClient>("catalog",
                new SearchClient(new Uri(searchConfig!.Endpoint!), salesIndexConfig!.CatalogIndexName!, credential));

            // EcommerceIndexService — creates/updates the catalog index at startup
            builder.Services.AddSingleton<EcommerceIndexService>(sp => new EcommerceIndexService(
                sp.GetRequiredService<SearchIndexClient>(),
                sp.GetRequiredKeyedService<SearchClient>("catalog"),
                sp.GetRequiredService<AzureOpenAIClient>(),
                sp.GetRequiredService<IOptions<SalesIndexSettings>>().Value,
                sp.GetRequiredService<IOptions<FoundrySettings>>().Value,
                sp.GetRequiredService<ILogger<EcommerceIndexService>>()));

            // Health check — lightweight probe confirms catalog index connectivity
            hcBuilder.Add(new HealthCheckRegistration(
                "azure-search-catalog",
                sp => new AzureSearchHealthCheck(sp.GetRequiredKeyedService<SearchClient>("catalog")),
                failureStatus: HealthStatus.Degraded,
                tags: ["catalog", "search"]));

            // ── SalesWorkflowAgent — 3-step sequential workflow ─────────────────
            // Step 1: catalog-retriever  → finds matching products via vector search
            // Step 2: stock-checker      → verifies availability for each product
            // Step 3: sales-responder    → synthesizes a customer-facing recommendation
            builder.AddAIAgent("SalesWorkflowAgent", (sp, name) =>
            {
                var azClient = sp.GetRequiredService<AzureOpenAIClient>();
                var settings = sp.GetRequiredService<IOptions<FoundrySettings>>().Value;
                var catalogClient = sp.GetRequiredKeyedService<SearchClient>("catalog");
                var repo = sp.GetRequiredService<IProductRepository>();

                var catalogRetriever = azClient
                    .GetChatClient(settings.Deployment!)
                    .AsAIAgent(
                        instructions: "Use the catalog_search tool to find products matching the customer's request. Return the full list of matching products with all details.",
                        name: "catalog-retriever",
                        tools: [CatalogSearchTool.Create(
                            catalogClient,
                            azClient,
                            sp.GetRequiredService<IOptions<SalesIndexSettings>>().Value,
                            settings)]);

                var stockChecker = azClient
                    .GetChatClient(settings.Deployment!)
                    .AsAIAgent(
                        instructions: "For each product mentioned in the previous message, use the stock_check tool to verify its current availability and price. Report the results.",
                        name: "stock-checker",
                        tools: [StockCheckTool.Create(repo)]);

                var salesResponder = azClient
                    .GetChatClient(settings.Deployment!)
                    .AsAIAgent(
                        instructions: "You are a friendly sales assistant. Using the catalog details and stock information provided, write a helpful recommendation for the customer. For each product include: name, key specs, price, and availability. Flag Low Stock items. Suggest alternatives for Out of Stock products.",
                        name: "sales-responder");

                var workflow = AgentWorkflowBuilder.BuildSequential(name, [catalogRetriever, stockChecker, salesResponder]);
                return workflow.AsAIAgent(name, name,
                    "Sequential sales workflow: catalog-retriever → stock-checker → sales-responder.",
                    InProcessExecution.OffThread,
                    includeExceptionDetails: false,
                    includeWorkflowOutputsInResponse: false);
            }, ServiceLifetime.Singleton);
        }

        return builder;
    }
}
