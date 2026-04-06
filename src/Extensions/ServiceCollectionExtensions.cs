using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using SalesWorkflow.Agents;
using SalesWorkflow.Configuration;
using SalesWorkflow.Data;
using SalesWorkflow.Services;
using SalesWorkflow.Infrastructure;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

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

        // These agents depend only on IChatClient + in-memory repos — always registered
        AddCustomerServiceAgent(builder);
        AddAfterSaleReportAgent(builder);

        // Orchestrators registered last so keyed participants are already in the container
        AddOrchestratorAgent(builder, catalogConfigured);
        AddBackOfficeOrchestratorAgent(builder);

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
            .AddOptions<BackOfficeSettings>()
            .Bind(builder.Configuration.GetSection("BackOffice"));

        builder.Services
            .AddOptions<AzureSearchSettings>()
            .Bind(builder.Configuration.GetSection("AzureSearch"));

        builder.Services
            .AddOptions<SalesIndexSettings>()
            .Bind(builder.Configuration.GetSection("SalesIndex"));

        // IProductRepository — always available; StockCheckTool works without Azure Search
        builder.Services.AddSingleton<IProductRepository, ProductRepository>();

        // In-memory order and customer stores for CustomerService and AfterSaleReport agents
        builder.Services.AddSingleton<IOrderRepository, OrderRepository>();
        builder.Services.AddSingleton<ICustomerRepository, CustomerRepository>();

        // Conversation history store — keyed by sessionId, accumulates ChatMessage turns.
        // Replace InMemoryConversationHistoryStore with a Redis/Cosmos/SQL implementation
        // for production multi-instance deployments (one DI line change).
        builder.Services.AddSingleton<IConversationHistoryStore, InMemoryConversationHistoryStore>();

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

        // IChatClient middleware pipeline (outermost → innermost):
        //   UseLogging          — logs every request/response via LoggingChatClient
        //   UseFunctionInvocation — executes tool calls returned by the LLM and re-submits
        //                          the tool results automatically before returning to callers.
        //                          Required: AsAIAgent() does not invoke tools itself; without
        //                          this middleware, parallel or sequential tool_calls returned
        //                          by the LLM are left unexecuted in the history, causing
        //                          HTTP 400 "assistant message with tool_calls must be followed
        //                          by tool messages" from Azure OpenAI on the next LLM call.
        builder.Services.AddSingleton<IChatClient>(sp =>
            sp.GetRequiredService<AzureOpenAIClient>()
                .GetChatClient(foundrySettings.Deployment!)
                .AsIChatClient()
                .AsBuilder()
                .UseLogging(sp.GetRequiredService<ILoggerFactory>())
                .UseFunctionInvocation()
                .Build());
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

    private static void AddCustomerServiceAgent(WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<CustomerServiceWorkflowAgent>();

        // ── CustomerServiceWorkflowAgent — Handoff workflow ──────────────────
        // triage-agent classifies intent and hands off to a specialist:
        //   billing-specialist  → refunds, disputes, escalation (HITL)
        //   shipping-specialist → delivery tracking, address issues
        // EnableReturnToPrevious() lets a specialist re-route back to triage.
        builder.AddAIAgent(
            CustomerServiceWorkflowAgent.AgentName,
            (sp, name) => sp.GetRequiredService<CustomerServiceWorkflowAgent>().CreateAgent(name),
            ServiceLifetime.Singleton);
    }

    private static void AddAfterSaleReportAgent(WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<AfterSaleReportWorkflowAgent>();

        // ── AfterSaleReportWorkflowAgent — Concurrent workflow ──────────────
        // sales-analyst and satisfaction-analyst run in parallel (fan-out);
        // the aggregator lambda merges their outputs into one admin report (fan-in).
        builder.AddAIAgent(
            AfterSaleReportWorkflowAgent.AgentName,
            (sp, name) => sp.GetRequiredService<AfterSaleReportWorkflowAgent>().CreateAgent(name),
            ServiceLifetime.Singleton);
    }

    private static void AddOrchestratorAgent(WebApplicationBuilder builder, bool hasCatalog)
    {
        builder.Services.AddSingleton<OrchestratorAgent>();

        // ── OrchestratorAgent — GroupChat workflow ────────────────────────
        // OrchestratorGroupChatManager uses the LLM to classify intent and
        // selects the appropriate sub-workflow as the next GroupChat speaker.
        // Participants are resolved from DI at factory time; SalesWorkflowAgent
        // is only included when the catalog index is configured.
        builder.AddAIAgent(
            OrchestratorAgent.AgentName,
            (sp, name) =>
            {
                // Build a dedicated CustomerService agent without EnableReturnToPrevious so
                // the handoff switch graph does not trigger a TargetSite serialization error
                // when exceptions propagate through the GroupChat pipeline.
                var customerServiceForOrchestrator = sp.GetRequiredService<CustomerServiceWorkflowAgent>()
                    .CreateAgent(CustomerServiceWorkflowAgent.AgentName, runningAsGroupChatParticipant: true);

                var participants = new List<AIAgent>
                {
                    customerServiceForOrchestrator,
                };

                if (hasCatalog)
                    participants.Add(sp.GetRequiredKeyedService<AIAgent>(SalesWorkflowAgent.AgentName));

                return sp.GetRequiredService<OrchestratorAgent>().CreateAgent(
                    name,
                    participants,
                    sp.GetRequiredService<ILogger<OrchestratorGroupChatManager>>());
            },
            ServiceLifetime.Singleton);
    }

    private static void AddBackOfficeOrchestratorAgent(WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<BackOfficeOrchestratorAgent>();

        // ── BackOfficeOrchestratorAgent — GroupChat workflow (admin-only) ──
        // Participants: AfterSaleReportWorkflowAgent (always available).
        // Exposed via POST /admin/agents, protected by API key auth.
        builder.AddAIAgent(
            BackOfficeOrchestratorAgent.AgentName,
            (sp, name) =>
            {
                var participants = new List<AIAgent>
                {
                    sp.GetRequiredKeyedService<AIAgent>(AfterSaleReportWorkflowAgent.AgentName),
                };

                return sp.GetRequiredService<BackOfficeOrchestratorAgent>().CreateAgent(
                    name,
                    participants,
                    sp.GetRequiredService<ILogger<OrchestratorGroupChatManager>>());
            },
            ServiceLifetime.Singleton);
    }
}
