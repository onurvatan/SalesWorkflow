using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using SalesWorkflow.Agents;
using SalesWorkflow.Configuration;
using SalesWorkflow.Data;
using SalesWorkflow.Services;
using SalesWorkflow.Infrastructure;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

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

        // IChatClient — pre-configured with the model deployment for sub-agent construction
        builder.Services.AddSingleton<IChatClient>(sp =>
            sp.GetRequiredService<AzureOpenAIClient>()
                .GetChatClient(foundrySettings.Deployment!)
                .AsIChatClient());

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

            // CatalogIndexService — creates/updates the catalog index at startup
            builder.Services.AddSingleton<CatalogIndexService>(sp => new CatalogIndexService(
                sp.GetRequiredService<SearchIndexClient>(),
                sp.GetRequiredKeyedService<SearchClient>("catalog"),
                sp.GetRequiredService<AzureOpenAIClient>(),
                sp.GetRequiredService<IOptions<SalesIndexSettings>>().Value,
                sp.GetRequiredService<IOptions<FoundrySettings>>().Value,
                sp.GetRequiredService<ILogger<CatalogIndexService>>()));

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
            builder.AddAIAgent("SalesWorkflowAgent", SalesWorkflowAgent.Create, ServiceLifetime.Singleton);
        }

        return builder;
    }
}
