# AgentCommerce

A .NET 10 Web API for an AI-powered e-commerce sales assistant built with the **Microsoft Agent Framework** and **Azure AI Search** vector embeddings.

| Agent                | Endpoint                      | Description                                                                     |
| -------------------- | ----------------------------- | ------------------------------------------------------------------------------- |
| `SalesWorkflowAgent` | `POST /agents/sales-workflow` | 3-step sequential workflow: catalog-retriever → stock-checker → sales-responder |

Both agents are visible in the **Agent Framework DevUI** at `/devui` (development only).

<img width="2735" height="1491" alt="image" src="https://github.com/user-attachments/assets/7639ee81-c61e-402a-8997-4966910224f5" />

See [docs/sales-workflow.md](docs/sales-workflow.md) for full architecture diagrams, tool schemas, and configuration reference.

---

## Project Structure

```
src/
  Configuration/
    FoundrySettings.cs         — Azure OpenAI endpoint + deployment names
    AzureSearchSettings.cs     — search service endpoint
    SalesIndexSettings.cs      — catalog index name, vector dimensions, field names
  Data/
    ProductRepository.cs       — IProductRepository + 15 seeded electronics products
  Models/
    Product.cs                 — domain model (Sku, Name, Brand, Price, StockQuantity, Tags…)
  Tools/
    CatalogSearchTool.cs       — AIFunction: embed query → vector k-NN search on product-catalog
    StockCheckTool.cs          — AIFunction: in-memory stock lookup with availability status
  Services/
    EcommerceIndexService.cs   — creates product-catalog index if absent; embeds + uploads products
  Infrastructure/
    AzureSearchHealthCheck.cs  — IHealthCheck: lightweight 0-result probe on catalog index
  Extensions/
    ServiceCollectionExtensions.cs — AddSalesWorkflowApp() — all DI wiring in one call
Program.cs                     — minimal API, catalog startup indexing, two endpoints
docs/
  sales-workflow.md            — full guide: prerequisites, architecture, endpoints, config
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

| Endpoint                 | Method | Description                                     |
| ------------------------ | ------ | ----------------------------------------------- |
| `/agents/sales`          | POST   | SalesAgent — single turn, two tools             |
| `/agents/sales-workflow` | POST   | SalesWorkflowAgent — 3-step sequential workflow |
| `/health`                | GET    | Catalog index connectivity probe                |

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
