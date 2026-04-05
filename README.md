# AgentCommerce

A .NET 10 Web API for an AI-powered e-commerce sales assistant built with the **Microsoft Agent Framework** and **Azure AI Search** vector embeddings.

| Agent                          | Endpoint                         | Pattern    | Description                                                               |
| ------------------------------ | -------------------------------- | ---------- | ------------------------------------------------------------------------- |
| `SalesWorkflowAgent`           | `POST /agents/sales-workflow`    | Sequential | 3-step pipeline: catalog-retriever → stock-checker → sales-responder      |
| `CustomerServiceWorkflowAgent` | `POST /agents/customer-service`  | Handoff    | triage-agent routes to billing-specialist or shipping-specialist          |
| `AfterSaleReportWorkflowAgent` | `POST /agents/after-sale-report` | Concurrent | sales-analyst ∥ satisfaction-analyst → merged admin report                |
| `OrchestratorAgent`            | `POST /agents`                   | GroupChat  | LLM-driven routing to CustomerService \| AfterSaleReport \| SalesWorkflow |

Both agents are visible in the **Agent Framework DevUI** at `/devui` (development only).

<img width="2735" height="1491" alt="image" src="https://github.com/user-attachments/assets/7639ee81-c61e-402a-8997-4966910224f5" />

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
    OrchestratorAgent.cs           — GroupChat: LLM-driven routing to the correct workflow agent
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

---

## API Reference

| Endpoint                    | Method | Pattern    | Description                                                          |
| --------------------------- | ------ | ---------- | -------------------------------------------------------------------- |
| `/agents/sales`             | POST   | —          | SalesAgent — single turn, two tools                                  |
| `/agents/sales-workflow`    | POST   | Sequential | 3-step pipeline: catalog-retriever → stock-checker → sales-responder |
| `/agents/customer-service`  | POST   | Handoff    | triage-agent → billing-specialist \| shipping-specialist             |
| `/agents/after-sale-report` | POST   | Concurrent | sales-analyst ∥ satisfaction-analyst → merged admin report           |
| `/agents`                   | POST   | GroupChat  | Orchestrator — LLM routes to the correct workflow agent              |
| `/health`                   | GET    | —          | Catalog index connectivity probe                                     |

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
