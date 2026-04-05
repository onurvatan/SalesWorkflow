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

        var credential = builder.Environment.IsDevelopment()
            ? (Azure.Core.TokenCredential)new AzureCliCredential()
            : new DefaultAzureCredential();

        AddCommonServices(builder);
        AddAzureClients(builder, foundrySettings, credential);

        bool catalogConfigured = !string.IsNullOrEmpty(searchConfig?.Endpoint)
                              && !string.IsNullOrEmpty(salesIndexConfig?.CatalogIndexName);
        if (catalogConfigured)
        {
            AddCatalogSearch(builder, searchConfig!, salesIndexConfig!, credential);
            AddSalesWorkflowAgent(builder);
        }

        return builder;
    }

    private static void AddCommonServices(WebApplicationBuilder builder)
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

        // IProductRepository — always available; StockCheckTool works without Azure Search
        builder.Services.AddSingleton<IProductRepository, ProductRepository>();

        builder.Services.AddHealthChecks();
    }

    private static void AddAzureClients(
        WebApplicationBuilder builder,
        FoundrySettings foundrySettings,
        Azure.Core.TokenCredential credential)
    {
        // AzureCliCredential in Development for reliability, DefaultAzureCredential in Production
        builder.Services.AddSingleton(
            new AzureOpenAIClient(new Uri(foundrySettings.Endpoint!), credential));

        // IChatClient — pre-configured with the model deployment for sub-agent construction
        builder.Services.AddSingleton<IChatClient>(sp =>
            sp.GetRequiredService<AzureOpenAIClient>()
                .GetChatClient(foundrySettings.Deployment!)
                .AsIChatClient());
    }

    private static void AddCatalogSearch(
        WebApplicationBuilder builder,
        AzureSearchSettings searchConfig,
        SalesIndexSettings salesIndexConfig,
        Azure.Core.TokenCredential credential)
    {
        // SearchIndexClient — admin plane (index management)
        builder.Services.AddSingleton(
            new SearchIndexClient(new Uri(searchConfig.Endpoint!), credential));

        // Keyed SearchClient for the catalog index
        builder.Services.AddKeyedSingleton<SearchClient>("catalog",
            new SearchClient(new Uri(searchConfig.Endpoint!), salesIndexConfig.CatalogIndexName!, credential));

        // CatalogIndexService — creates/updates the catalog index at startup
        builder.Services.AddSingleton<CatalogIndexService>(sp => new CatalogIndexService(
            sp.GetRequiredService<SearchIndexClient>(),
            sp.GetRequiredKeyedService<SearchClient>("catalog"),
            sp.GetRequiredService<AzureOpenAIClient>(),
            sp.GetRequiredService<IOptions<SalesIndexSettings>>().Value,
            sp.GetRequiredService<IOptions<FoundrySettings>>().Value,
            sp.GetRequiredService<ILogger<CatalogIndexService>>()));

        // Health check — lightweight probe confirms catalog index connectivity
        builder.Services.AddHealthChecks().Add(new HealthCheckRegistration(
            "azure-search-catalog",
            sp => new AzureSearchHealthCheck(sp.GetRequiredKeyedService<SearchClient>("catalog")),
            failureStatus: HealthStatus.Degraded,
            tags: ["catalog", "search"]));
    }

    private static void AddSalesWorkflowAgent(WebApplicationBuilder builder)
    {
        // Register as a concrete type so the DI container resolves its constructor
        // (including [FromKeyedServices("catalog")] on SearchClient)
        builder.Services.AddSingleton<SalesWorkflowAgent>();

        // ── SalesWorkflowAgent — 3-step sequential workflow ─────────────────
        // Step 1: catalog-retriever  → finds matching products via vector search
        // Step 2: stock-checker      → verifies availability for each product
        // Step 3: sales-responder    → synthesizes a customer-facing recommendation
        builder.AddAIAgent(
            SalesWorkflowAgent.AgentName,
            (sp, name) => sp.GetRequiredService<SalesWorkflowAgent>().CreateAgent(name),
            ServiceLifetime.Singleton);
    }
}
