# Pattern 4 & 5 — E-Commerce Sales Agent

This sample demonstrates a production-ready electronics catalog built on **Azure AI Search with vector embeddings**. The catalog is indexed at startup and queried by two agent patterns: a single agent that can call both tools in one turn, and a 3-step sequential workflow.

---

## Azure Prerequisites

This sample uses passwordless authentication (`AzureCliCredential` in Development, `DefaultAzureCredential` in Production). No API keys are required, but the identity running the app must have two RBAC roles on the Azure AI Search service.

### Step 1 — Enable RBAC Authentication on the Search Service

By default, Azure AI Search is created with `apiKeyOnly` auth, which **blocks Entra ID tokens** even when RBAC roles are assigned. You must explicitly enable it:

```powershell
az search service update `
  --name <your-search-service-name> `
  --resource-group <your-rg> `
  --auth-options aadOrApiKey `
  --aad-auth-failure-mode http403
```

Verify the change:

```powershell
az search service show --name <your-search-service-name> --resource-group <your-rg> --query "authOptions" -o json
# Expected: { "aadOrApiKey": { "aadAuthFailureMode": "http403" } }
```

> **Symptom if skipped**: The app starts but logs `Catalog indexing failed — Status: 403 (Forbidden)` even though roles are correctly assigned.

---

### Step 2 — Assign RBAC Roles

| Role                            | Purpose                                                   |
| ------------------------------- | --------------------------------------------------------- |
| `Search Service Contributor`    | Create and manage index definitions (`SearchIndexClient`) |
| `Search Index Data Contributor` | Upload documents and run queries (`SearchClient`)         |

### Assign Roles (Development)

Run once after `az login`:

```powershell
$userId   = az ad signed-in-user show --query id -o tsv
$searchId = az resource list `
              --resource-type "Microsoft.Search/searchServices" `
              --query "[?name=='<your-search-service-name>'].id" -o tsv

az role assignment create --role "Search Service Contributor"    --assignee $userId --scope $searchId
az role assignment create --role "Search Index Data Contributor" --assignee $userId --scope $searchId
```

Wait ~60 seconds for assignments to propagate before starting the app.

### Assign Roles (Production — Managed Identity)

```powershell
$principalId = az webapp identity show --name <app-name> --resource-group <rg> --query principalId -o tsv

az role assignment create --role "Search Service Contributor"    --assignee $principalId --scope $searchId
az role assignment create --role "Search Index Data Contributor" --assignee $principalId --scope $searchId
```

### Verify

```powershell
az role assignment list --assignee $userId --scope $searchId --query "[].roleDefinitionName" -o tsv
```

Expected output:

```
Search Index Data Contributor
Search Service Contributor
```

> **Note**: The Azure OpenAI resource (`Foundry:Endpoint`) also requires `Cognitive Services OpenAI User` on your identity if you haven't already configured it for the other agents in this sample.

---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  Startup — Catalog Indexing                                 │
│                                                             │
│  ProductRepository (in-memory, 15 products)                 │
│      │                                                      │
│      ▼  EcommerceIndexService                               │
│  EnsureIndexExistsAsync()  ──► Azure AI Search Index        │
│      │  (HNSW vector, semantic config)                      │
│      │                                                      │
│  IndexProductsAsync()                                       │
│      │  text-embedding-3-small                              │
│      ├──► embed(name + brand + category + description + tags)│
│      └──► MergeOrUploadDocumentsAsync()  (idempotent)       │
└─────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────┐
│  Pattern 4 — SalesAgent (single agent, two tools)                │
│                                                                  │
│  User prompt                                                     │
│      │                                                           │
│      ▼                                                           │
│  SalesAgent ──[catalog_search]──► CatalogSearchTool             │
│             ──[stock_check]────► StockCheckTool                  │
│             (LLM may call both tools in a single response turn)  │
│      │                                                           │
│      ▼                                                           │
│  Final response: products + specs + price + availability         │
└──────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────┐
│  Pattern 5 — SalesWorkflowAgent (3-step sequential workflow)     │
│                                                                  │
│  User prompt                                                     │
│      │                                                           │
│      ▼                                                           │
│  ┌──────────────────────────────┐                               │
│  │  catalog-retriever           │  calls catalog_search         │
│  │  → finds matching products   │  returns raw product list     │
│  └──────────────┬───────────────┘                               │
│                 │                                                 │
│                 ▼                                                 │
│  ┌──────────────────────────────┐                               │
│  │  stock-checker               │  calls stock_check for each   │
│  │  → verifies availability     │  product in previous output   │
│  └──────────────┬───────────────┘                               │
│                 │                                                 │
│                 ▼                                                 │
│  ┌──────────────────────────────┐                               │
│  │  sales-responder             │  no tools — synthesizes       │
│  │  → customer-facing reply     │  catalog + stock into reply   │
│  └──────────────┬───────────────┘                               │
│                 │                                                 │
│                 ▼                                                 │
│  Final response: friendly recommendation with price + stock      │
└──────────────────────────────────────────────────────────────────┘
```

---

## When to Use Which Pattern

|                  | SalesAgent                        | SalesWorkflowAgent               |
| ---------------- | --------------------------------- | -------------------------------- |
| **Tool calls**   | Parallel (LLM decides)            | Strictly sequential              |
| **Latency**      | Lower — one LLM round-trip        | Higher — three LLM round-trips   |
| **Transparency** | Less visible intermediate steps   | Full step-by-step trace in DevUI |
| **Use case**     | Production, low-latency responses | Demos, debugging, observability  |

---

## Catalog Index Schema

| Field           | Type                 | Attributes                         |
| --------------- | -------------------- | ---------------------------------- |
| `id`            | `String` (key)       | filterable                         |
| `sku`           | `String`             | searchable, filterable             |
| `name`          | `String`             | searchable, filterable, sortable   |
| `brand`         | `String`             | searchable, filterable, facetable  |
| `category`      | `String`             | searchable, filterable, facetable  |
| `description`   | `String`             | searchable                         |
| `price`         | `Double`             | filterable, sortable               |
| `currency`      | `String`             | —                                  |
| `stockQuantity` | `Int32`              | filterable, sortable               |
| `tags`          | `Collection(String)` | searchable, filterable, facetable  |
| `contentVector` | `Collection(Single)` | searchable, 1536-dim, HNSW profile |

**Vector algorithm**: HNSW (`product-hnsw`)  
**Vector profile**: `product-vector-profile`  
**Semantic config** (optional): name → description as content → tags, brand, category as keywords

---

## Tools

### `catalog_search`

Vector similarity search over the product catalog.

```
Input : query (string) — natural language describing desired product, features, budget, use case
Output: top-5 matching products as JSON-lines (sku, name, brand, category, price, currency, description, stockQuantity, tags)
```

**How it works**

1. Generates a query embedding via `text-embedding-3-small`
2. Runs `VectorizedQuery` (k-NN = 5) against `contentVector`
3. Optionally applies semantic re-ranking if `SalesIndex:SemanticConfigName` is configured

### `stock_check`

In-memory lookup against `IProductRepository`.

```
Input : query (string) — SKU, product name, or brand keyword
Output: JSON array with sku, name, price, currency, stockQuantity, availability
```

**Availability thresholds**

| `stockQuantity` | Status           |
| --------------- | ---------------- |
| 0               | `"Out of Stock"` |
| 1–4             | `"Low Stock"`    |
| ≥ 5             | `"In Stock"`     |

---

## Agent Instructions

### SalesAgent

```
You are a knowledgeable and friendly electronics sales assistant for TechShop.

When a customer asks about products, always:
1. Use catalog_search to find products matching their request.
2. Use stock_check to verify real-time availability and pricing.
You may call both tools within the same response turn.

In your reply:
- List each recommended product with name, key specs, price, and availability.
- Highlight "Low Stock" or "Out of Stock" items.
- Suggest in-stock alternatives for out-of-stock products.
- Never fabricate specs or prices — only report what the tools return.
```

### SalesWorkflowAgent steps

| Step | Agent               | Instructions                                                                                                                                                                       |
| ---- | ------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1    | `catalog-retriever` | Use `catalog_search` to find products. Return full list with all details.                                                                                                          |
| 2    | `stock-checker`     | For each product in the previous message, use `stock_check` to verify availability. Report results.                                                                                |
| 3    | `sales-responder`   | Synthesize catalog and stock info into a helpful, friendly recommendation. Include price, specs, availability for each product. Flag low/out-of-stock items. Suggest alternatives. |

---

## Endpoints

```
POST /agents/sales
```

Single-turn sales assistant. Calls `catalog_search` and `stock_check` as needed.

```http
POST /agents/sales
Content-Type: application/json

{ "input": "I need a laptop for video editing under $2000" }
```

**Response**

```json
{
  "agentName": "SalesAgent",
  "result": "Here are the best options for video editing under $2000:\n\n1. **Dell XPS 15 (2025)** — $1,799.99 | In Stock (12 units)\n   Intel Core Ultra 9, RTX 4070, 32 GB RAM, 1 TB SSD, 15.6\" OLED ..."
}
```

---

```
POST /agents/sales-workflow
```

3-step sequential workflow. Useful for debugging or when step-by-step traceability is required.

```http
POST /agents/sales-workflow
Content-Type: application/json

{ "input": "Which gaming laptops do you have in stock?" }
```

---

```
GET /health
```

Health check endpoint. Returns `200 Healthy` when the catalog index is reachable.

```json
{
  "status": "Healthy",
  "results": {
    "azure-search-catalog": {
      "status": "Healthy",
      "description": "Catalog index is reachable."
    }
  }
}
```

---

## Configuration

```json
// appsettings.json
{
  "AzureSearch": {
    "Endpoint": "https://YOUR_SEARCH.search.windows.net",
    "IndexName": "your-existing-index"
  },
  "SalesIndex": {
    "CatalogIndexName": "product-catalog",
    "SemanticConfigName": "",
    "VectorFieldName": "contentVector",
    "VectorDimensions": 1536
  }
}
```

- **`AzureSearch:Endpoint`** — shared with `CoreAgent`; both indexes live on the same Azure AI Search service.
- **`SalesIndex:CatalogIndexName`** — dedicated index for the product catalog (separate from `AzureSearch:IndexName`).
- **`SalesIndex:SemanticConfigName`** — leave empty for pure vector search; set to `"product-semantic"` for hybrid (semantic + vector).
- **`Foundry:EmbeddingDeployment`** — reused for both indexing and query embedding (default: `text-embedding-3-small`).

> If `SalesIndex:CatalogIndexName` is empty, the sales agents are not registered and catalog indexing is skipped. All existing agents continue to work normally.

---

## Startup Flow

```
Application starts
    │
    ├── RAG seeding (existing)
    │   └── Seeds in-memory EF Core DB with 5 framework docs
    │
    └── Catalog indexing (new)
        ├── EnsureIndexExistsAsync()
        │   ├── GET index from Azure AI Search
        │   ├── If 404 → create index with HNSW vector + fields + optional semantic config
        │   └── If exists → skip (idempotent)
        └── IndexProductsAsync(15 products)
            ├── For each product: embed(name + brand + description + tags) via text-embedding-3-small
            ├── Build SearchDocument with vector field
            └── MergeOrUploadDocumentsAsync() — safe to call on every restart
```

---

## Key Files

| File                                            | Purpose                                          |
| ----------------------------------------------- | ------------------------------------------------ |
| `src/Models/Product.cs`                         | Domain model                                     |
| `src/Data/ProductRepository.cs`                 | In-memory product store + `IProductRepository`   |
| `src/Configuration/SalesIndexSettings.cs`       | `SalesIndex` config section binding              |
| `src/Ecommerce/EcommerceIndexService.cs`        | Index lifecycle + product embedding + upload     |
| `src/Tools/CatalogSearchTool.cs`                | `catalog_search` AIFunction                      |
| `src/Tools/StockCheckTool.cs`                   | `stock_check` AIFunction                         |
| `src/Agents/SalesAgent.cs`                      | Agent name + instructions                        |
| `src/Infrastructure/AzureSearchHealthCheck.cs`  | `IHealthCheck` for catalog index                 |
| `src/Extensions/ServiceCollectionExtensions.cs` | DI wiring (additive — existing agents untouched) |
| `Program.cs`                                    | Startup indexing block + endpoints               |
