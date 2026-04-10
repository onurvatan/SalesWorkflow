# AgentCommerce

A .NET 10 Web API for an AI-powered e-commerce sales assistant built with the **Microsoft Agent Framework** and **Azure AI Search** vector embeddings.

| Agent                          | Endpoint                         | Pattern    | Description                                                                                        |
| ------------------------------ | -------------------------------- | ---------- | -------------------------------------------------------------------------------------------------- |
| `ClientOrchestratorAgent`      | `POST /agents`                   | GroupChat  | **Production (client-facing) entry point.** LLM-driven routing to CustomerService \| SalesWorkflow |
| `BackOfficeOrchestratorAgent`  | `POST /admin/agents`             | GroupChat  | **Admin entry point (API-key protected).** Routes admin prompts to AfterSaleReportWorkflowAgent    |
| `SalesWorkflowAgent`           | `POST /agents/sales-workflow`    | Sequential | 3-step pipeline: catalog-retriever → stock-checker → sales-responder                               |
| `CustomerServiceWorkflowAgent` | `POST /agents/customer-service`  | Handoff    | triage-agent routes to billing-specialist or shipping-specialist                                   |
| `AfterSaleReportWorkflowAgent` | `POST /agents/after-sale-report` | Concurrent | sales-analyst ∥ satisfaction-analyst → merged admin report                                         |

All agents are visible in the **Agent Framework DevUI** at `/devui` (development only).

<img width="2735" height="1491" alt="image" src="https://github.com/user-attachments/assets/7639ee81-c61e-402a-8997-4966910224f5" />

---

## Architecture

The codebase is organized around three layers that map directly to the Microsoft Agent Framework model.

### Workflows

A **workflow** is the wiring between sub-agents — it defines the topology (who runs when) and the termination condition. Each workflow class owns that topology; it has no HTTP concerns of its own.

| Class                          | Pattern        | Topology                                                                                                                                                                                                                                                                                                                     |
| ------------------------------ | -------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ClientOrchestratorAgent`      | **GroupChat**  | **Production (client-facing) entry point.** `OrchestratorGroupChatManager` sends a routing prompt to the LLM at each turn; the model replies with the name of the participant to invoke next (`CustomerServiceWorkflowAgent` or `SalesWorkflowAgent`). Terminates after 3 iterations or when the response contains `[DONE]`. |
| `SalesWorkflowAgent`           | **Sequential** | `catalog-retriever` → `stock-checker` → `sales-responder` — each step receives the previous step's output as its input; the chain terminates after the final step.                                                                                                                                                           |
| `CustomerServiceWorkflowAgent` | **Handoff**    | `triage-agent` classifies intent → hands off to `billing-specialist` or `shipping-specialist`. `EnableReturnToPrevious()` lets a specialist route back to triage for re-classification without restarting the session (disabled when running inside the Orchestrator GroupChat to avoid a serialization incompatibility).    |
| `AfterSaleReportWorkflowAgent` | **Concurrent** | `sales-analyst` ∥ `satisfaction-analyst` run in parallel (fan-out), and their outputs are merged by an aggregator delegate into a single admin report (fan-in).                                                                                                                                                              |

### Sub-agents

Each workflow is composed of **sub-agents** — lightweight `IChatClient` wrappers created with `chatClient.AsAIAgent(instructions, name, tools)`. A sub-agent has a fixed system prompt and an optional tool list; it has no memory of prior turns unless the framework passes history to it.

| Sub-agent              | Workflow        | Role                                                                                                        |
| ---------------------- | --------------- | ----------------------------------------------------------------------------------------------------------- |
| `catalog-retriever`    | Sales           | Calls `catalog_search`; returns matching products                                                           |
| `stock-checker`        | Sales           | Calls `stock_check` for each product; reports availability                                                  |
| `sales-responder`      | Sales           | Synthesises catalog + stock results into a customer-facing recommendation                                   |
| `triage-agent`         | CustomerService | Identifies customer + issue via `customer_lookup` + `order_status`; decides which specialist to hand off to |
| `billing-specialist`   | CustomerService | Handles refunds and billing disputes; calls `escalate_to_human` for high-value cases                        |
| `shipping-specialist`  | CustomerService | Handles delivery tracking and shipping issues via `order_status`                                            |
| `sales-analyst`        | AfterSaleReport | Calls `sales_report`; produces revenue and order-status summary                                             |
| `satisfaction-analyst` | AfterSaleReport | Calls `customer_satisfaction_report`; produces CSAT and at-risk customer summary                            |

### Tools

Tools are `AIFunction` instances exposed to sub-agents via the `tools: [...]` parameter. They are pure functions — no LLM calls, no side effects beyond the in-memory repositories.

| Tool                           | File                          | Used by                                                     | Description                                                                                                                                         |
| ------------------------------ | ----------------------------- | ----------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------- |
| `catalog_search`               | `CatalogSearchTool.cs`        | `catalog-retriever`                                         | Embeds the query with `text-embedding-3-small`, runs a vector k-NN search on the Azure AI Search `product-catalog` index, returns matching products |
| `stock_check`                  | `StockCheckTool.cs`           | `stock-checker`                                             | Looks up current stock quantity and price from the in-memory `IProductRepository`; returns `In Stock / Low Stock / Out of Stock`                    |
| `customer_lookup`              | `CustomerLookupTool.cs`       | `triage-agent`                                              | Finds a customer record by name or ID and returns their tier, satisfaction score, and recent order IDs                                              |
| `order_status`                 | `OrderStatusTool.cs`          | `triage-agent`, `billing-specialist`, `shipping-specialist` | Returns full order details (status, items, total, timestamps) by order ID or customer ID                                                            |
| `escalate_to_human`            | `EscalateTool.cs`             | `billing-specialist`                                        | HITL escalation stub — generates an `ESC-*` request ID and a simulated response time; extend this to connect to a real ticketing system             |
| `sales_report`                 | `SalesReportTool.cs`          | `sales-analyst`                                             | Aggregates order data: total revenue, status breakdown, top products by revenue                                                                     |
| `customer_satisfaction_report` | `CustomerSatisfactionTool.cs` | `satisfaction-analyst`                                      | Aggregates customer satisfaction scores, tier distribution, and at-risk customers (score ≤ 2)                                                       |

---

| Doc                                                                      | Pattern    | Description                                                         |
| ------------------------------------------------------------------------ | ---------- | ------------------------------------------------------------------- |
| [docs/sales-workflow.md](docs/sales-workflow.md)                         | Sequential | Architecture, catalog index schema, tool schemas, config reference  |
| [docs/customer-service-workflow.md](docs/customer-service-workflow.md)   | Handoff    | Triage topology, tool schemas, HITL escalation extension point      |
| [docs/after-sale-report-workflow.md](docs/after-sale-report-workflow.md) | Concurrent | Fan-out/fan-in topology, aggregator delegate, report format         |
| [docs/orchestrator-workflow.md](docs/orchestrator-workflow.md)           | GroupChat  | LLM routing logic, participant registration, when-to-use comparison |

---

## Project Structure

```
src/
  Configuration/
    FoundrySettings.cs         — Azure OpenAI endpoint + deployment names
    AzureSearchSettings.cs     — search service endpoint
    SalesIndexSettings.cs      — catalog index name, vector dimensions, field names
  Configuration/
    FoundrySettings.cs         — Azure OpenAI endpoint + deployment names
    AzureSearchSettings.cs     — search service endpoint
    SalesIndexSettings.cs      — catalog index name, vector dimensions, field names
  Data/
    ProductRepository.cs       — IProductRepository + 15 seeded electronics products
    OrderRepository.cs         — IOrderRepository + 10 seeded orders (Pending/Shipped/Delivered/Cancelled)
    CustomerRepository.cs      — ICustomerRepository + 5 seeded customers with satisfaction scores
  Models/
    Product.cs                 — domain model (Sku, Name, Brand, Price, StockQuantity, Tags…)
    Order.cs                   — Order + OrderStatus enum
    Customer.cs                — Customer + CustomerTier enum
  Tools/
    CatalogSearchTool.cs       — AIFunction: embed query → vector k-NN search on product-catalog
    StockCheckTool.cs          — AIFunction: in-memory stock lookup with availability status
    CustomerLookupTool.cs      — AIFunction: look up customer profile + recent orders
    OrderStatusTool.cs         — AIFunction: retrieve order details by Order ID or Customer ID
    EscalateTool.cs            — AIFunction: HITL escalation stub (returns ESC-* request ID)
    SalesReportTool.cs         — AIFunction: revenue summary, status breakdown, top products
    CustomerSatisfactionTool.cs — AIFunction: CSAT scores, tier breakdown, at-risk customers
  Agents/
    SalesWorkflowAgent.cs      — Sequential: catalog-retriever → stock-checker → sales-responder
    CustomerServiceWorkflowAgent.cs — Handoff: triage-agent → billing-specialist | shipping-specialist
    AfterSaleReportWorkflowAgent.cs — Concurrent: sales-analyst ∥ satisfaction-analyst → merged report
    ClientOrchestratorAgent.cs     — GroupChat: LLM-driven routing to the correct workflow agent (client-facing)
  Services/
    EcommerceIndexService.cs   — creates product-catalog index if absent; embeds + uploads products
  Infrastructure/
    AzureSearchHealthCheck.cs  — IHealthCheck: lightweight 0-result probe on catalog index
  Extensions/
    ServiceCollectionExtensions.cs — AddSalesWorkflowApp() — all DI wiring in one call
Program.cs                     — minimal API, catalog startup indexing, five endpoints
docs/
  sales-workflow.md                — Sequential pattern: prerequisites, architecture, catalog schema, config
  customer-service-workflow.md     — Handoff pattern: triage topology, HITL escalation, seeded data
  after-sale-report-workflow.md    — Concurrent pattern: fan-out/fan-in, aggregator delegate, report format
  orchestrator-workflow.md         — GroupChat pattern: LLM routing, participant registration, termination
```

---

## Prerequisites

- .NET 10 SDK
- Azure OpenAI resource with `gpt-4o` and `text-embedding-3-small` deployments
- Azure AI Search service
- Azure CLI logged in (`az login`) — used for passwordless auth in Development

### Azure AI Search — Required One-Time Setup

**Step 1 — Enable RBAC authentication** (the service defaults to API-key-only, which blocks Entra ID tokens):

```powershell
az search service update `
  --name <your-search-service-name> `
  --resource-group <your-rg> `
  --auth-options aadOrApiKey `
  --aad-auth-failure-mode http403
```

> **Symptom if skipped**: App logs `Catalog indexing failed — Status: 403 (Forbidden)` even with roles assigned.

**Step 2 — Assign RBAC roles** to your developer identity:

```powershell
$userId   = az ad signed-in-user show --query id -o tsv
$searchId = az resource list `
              --resource-type "Microsoft.Search/searchServices" `
              --query "[?name=='<your-search-service-name>'].id" -o tsv

az role assignment create --role "Search Service Contributor"    --assignee $userId --scope $searchId
az role assignment create --role "Search Index Data Contributor" --assignee $userId --scope $searchId
```

Wait ~60 seconds for propagation before starting the app. The `product-catalog` index is created automatically on first run.

---

## Configuration

Edit **`appsettings.Development.json`**:

```json
{
  "Foundry": {
    "Endpoint": "https://<your-resource>.openai.azure.com/",
    "Deployment": "gpt-4o",
    "EmbeddingDeployment": "text-embedding-3-small"
  },
  "AzureSearch": {
    "Endpoint": "https://<your-search>.search.windows.net"
  },
  "SalesIndex": {
    "CatalogIndexName": "product-catalog",
    "SemanticConfigName": "",
    "VectorFieldName": "contentVector",
    "VectorDimensions": 1536
  }
}
```

| Key                             | Required | Notes                                                                |
| ------------------------------- | -------- | -------------------------------------------------------------------- |
| `Foundry:Endpoint`              | Yes      | Azure OpenAI resource URL                                            |
| `Foundry:Deployment`            | Yes      | Chat model (default: `gpt-4o`)                                       |
| `Foundry:EmbeddingDeployment`   | Yes      | Embedding model (default: `text-embedding-3-small`)                  |
| `AzureSearch:Endpoint`          | Yes      | Search service URL                                                   |
| `SalesIndex:CatalogIndexName`   | Yes      | Index is auto-created if absent; leave empty to skip sales agents    |
| `SalesIndex:SemanticConfigName` | No       | Leave empty for pure vector search; set for hybrid semantic + vector |

---

## Running

```bash
az login          # authenticate once
dotnet run
```

- **DevUI** → `https://localhost:57500/devui`
- **Swagger** → `https://localhost:57500/swagger`
- **Health** → `https://localhost:57500/health`

On first startup, `EcommerceIndexService` creates the `product-catalog` index and uploads all 15 products with vector embeddings. Subsequent restarts are idempotent (`MergeOrUpload`).

## Back-Office Orchestrator (admin)

- **Agent:** `BackOfficeOrchestratorAgent`
- **Endpoint:** `POST /admin/agents` (requires `X-Api-Key` header)
- **Pattern:** GroupChat (admin)
- **Description:** Routes back-office/admin prompts to the `AfterSaleReportWorkflowAgent`. The admin endpoints are protected by an API key defined in `BackOffice:ApiKey` in `appsettings.Development.json` (default `dev-backoffice-key-12345` in the sample config).

Run & test (local):

1. Start the app locally:

```powershell
dotnet run
```

2. Call the back-office agent (example):

```powershell
$apiKey = 'dev-backoffice-key-12345'
$body = '{ "input": "Generate the monthly after-sale report" }'
Invoke-RestMethod -Method Post -Uri https://localhost:57500/admin/agents -Body $body -ContentType 'application/json' -Headers @{ 'X-Api-Key' = $apiKey }
```

Or with `curl`:

```bash
curl -k -H "Content-Type: application/json" -H "X-Api-Key: dev-backoffice-key-12345" \
  -d '{ "input": "Generate the monthly after-sale report" }' \
  https://localhost:57500/admin/agents
```

3. Run the unit tests for the project (includes `BackOfficeOrchestratorAgent` tests):

```bash
dotnet test
```

Notes and rename suggestion:

- `AfterSaleReportWorkflowAgent` was intentionally moved under the back-office orchestrator since it's an admin/reporting workflow. The existing customer-facing orchestrator has been renamed to `ClientOrchestratorAgent` (routes customer prompts to `CustomerServiceWorkflowAgent` and `SalesWorkflowAgent`). If you prefer a different name, consider `CustomerOrchestratorAgent` or `FrontOfficeOrchestratorAgent` and update the `AgentName` constants and DI registration in `Program.cs` and `src/Agents/*.cs` accordingly.

## API Reference

> **Production vs. testing:** In production, send all requests to `POST /agents` — the `ClientOrchestratorAgent` inspects the message and routes it to the right workflow automatically. The individual workflow endpoints (`/agents/sales-workflow`, `/agents/customer-service`, `/agents/after-sale-report`) bypass routing and invoke a single workflow directly; they exist for isolated testing and development only.

| Endpoint                    | Method | Pattern    | Description                                                                                                    |
| --------------------------- | ------ | ---------- | -------------------------------------------------------------------------------------------------------------- |
| `/agents`                   | POST   | GroupChat  | **Production.** Orchestrator — LLM routes to the correct workflow agent                                        |
| `/agents/sales-workflow`    | POST   | Sequential | **Testing.** 3-step pipeline: catalog-retriever → stock-checker → sales-responder                              |
| `/agents/customer-service`  | POST   | Handoff    | **Testing.** triage-agent → billing-specialist \| shipping-specialist                                          |
| `/agents/after-sale-report` | POST   | Concurrent | **Testing.** sales-analyst ∥ satisfaction-analyst → merged admin report                                        |
| `/agents/sales`             | POST   | —          | **Testing.** SalesAgent — single turn, two tools                                                               |
| `/health`                   | GET    | —          | Catalog index connectivity probe                                                                               |
| `/admin/agents`             | POST   | GroupChat  | **Admin (API-key protected).** Back-office orchestrator — routes admin prompts to AfterSaleReportWorkflowAgent |

### Request / Response

```http
POST /agents/sales
Content-Type: application/json

{ "input": "I need a laptop for video editing under $2000" }
```

```json
{
  "agentName": "SalesAgent",
  "result": "Here are the best options for video editing under $2000:\n\n1. **Dell XPS 15 (2025)** — $1,799.99 | In Stock (12 units)..."
}
```

```http
POST /agents/sales-workflow
Content-Type: application/json

{ "input": "Which gaming laptops do you have in stock?" }
```

---

## Startup Flow

```
Application starts
    │
    └── Catalog indexing
        ├── EnsureIndexExistsAsync()
        │   ├── GET index → if 404, create with HNSW vector fields + optional semantic config
        │   └── If exists → skip (idempotent)
        └── IndexProductsAsync(15 products)
            ├── For each product: embed(name + brand + description + tags) via text-embedding-3-small
            └── MergeOrUploadDocumentsAsync() — safe to call on every restart
```

If `SalesIndex:CatalogIndexName` is empty, indexing is skipped and the sales agents are not registered. The app still starts and `/health` returns healthy.
