# After-Sale Report Workflow — Concurrent Pattern

`POST /agents/after-sale-report`

The `AfterSaleReportWorkflowAgent` demonstrates the **Concurrent** (fan-out / fan-in) workflow pattern from [Microsoft.Agents.AI.Workflows](https://www.nuget.org/packages/Microsoft.Agents.AI.Workflows). Two analyst agents execute **in parallel** — `sales-analyst` and `satisfaction-analyst` — and the workflow's aggregator delegate merges their outputs into a single admin report.

---

## Topology

```
Admin request
      │
      ▼
┌──────────────────────────────────────────────────────┐
│  fan-out (parallel execution)                        │
│                                                      │
│  ┌─────────────────────────┐  ┌───────────────────┐ │
│  │  sales-analyst          │  │  satisfaction-    │ │
│  │  tool: sales_report     │  │  analyst          │ │
│  │  → revenue, orders,     │  │  tool:            │ │
│  │    top products         │  │  customer_satis-  │ │
│  │                         │  │  faction_report   │ │
│  │                         │  │  → CSAT, at-risk  │ │
│  └───────────┬─────────────┘  └──────────┬────────┘ │
│              │                           │           │
│              └──────────┬────────────────┘           │
│                         ▼                            │
│              aggregator delegate                     │
│              → merges both sections                  │
└──────────────────────────────────────────────────────┘
      │
      ▼
# After-Sale Admin Report
## Sales Analysis      (from sales-analyst)
## Customer Satisfaction Analysis  (from satisfaction-analyst)
```

Both agents share the same `ChatClient` but execute their tool calls and LLM inference concurrently. The aggregator is a pure C# delegate — no extra LLM call is made to merge the outputs.

---

## Agents

### `sales-analyst`

| Property        | Value                                                    |
|----------------|----------------------------------------------------------|
| Tool            | `sales_report`                                           |
| Responsibility  | Revenue metrics, order volume, top-selling products      |
| Output          | Bullet-point summary for admin dashboard                 |

**Instructions (excerpt)**

> Summarise: total orders, total revenue, status breakdown (Pending/Shipped/Delivered/Cancelled), and the top-selling products by revenue. Flag any anomalies such as high cancellation rates. Use bullet points and currency formatting.

---

### `satisfaction-analyst`

| Property        | Value                                                    |
|----------------|----------------------------------------------------------|
| Tool            | `customer_satisfaction_report`                           |
| Responsibility  | CSAT scores, tier breakdown, at-risk customers          |
| Output          | Bullet-point summary with at-risk customers highlighted  |

**Instructions (excerpt)**

> Summarise: average satisfaction score, score distribution, customers by tier, and the list of at-risk customers (score ≤ 2) who need immediate follow-up. Classify the overall NPS category. Highlight at-risk customers prominently.

---

## Tools

### `sales_report`

```
Input : dateRange? (string) — optional description, e.g. "Q1 2026" (currently returns full in-memory summary)
Output: JSON — sales summary
```

```json
{
  "reportType": "Sales Summary",
  "dateRange": "All time",
  "totalOrders": 10,
  "totalRevenue": 12949.90,
  "currency": "USD",
  "ordersByStatus": { "Delivered": 7, "Shipped": 2, "Pending": 1, "Cancelled": 1 },
  "topProducts": [
    { "sku": "APPLE-MBP14-M4", "name": "MacBook Pro 14 M4", "units": 3, "revenue": 5999.97 },
    { "sku": "DELL-XPS15-2025", "name": "Dell XPS 15 (2025)", "units": 2, "revenue": 3599.98 }
  ]
}
```

### `customer_satisfaction_report`

```
Input : tierFilter? (string) — optional: "Standard", "Premium", or "VIP"
Output: JSON — CSAT summary
```

```json
{
  "reportType": "Customer Satisfaction Summary",
  "tierFilter": "All tiers",
  "totalCustomers": 5,
  "averageScore": 3.8,
  "scoreDistribution": { "5": 2, "4": 1, "3": 1, "2": 1 },
  "customersByTier": { "Premium": 2, "Standard": 2, "VIP": 1 },
  "atRiskCustomers": ["David Lee (CUST-004) — Score: 2"],
  "atRiskCount": 1,
  "npsCategory": "Good"
}
```

**NPS category thresholds**

| Average Score | Category    |
|---------------|-------------|
| ≥ 4.5         | `Excellent` |
| ≥ 3.5         | `Good`      |
| ≥ 2.5         | `Neutral`   |
| < 2.5         | `At Risk`   |

---

## Aggregator Delegate

The aggregator is a C# lambda — not an LLM call — so it adds no latency or cost:

```csharp
aggregator: results =>
{
    var sections = results.Select((msgs, i) =>
    {
        var label = i == 0 ? "## Sales Analysis" : "## Customer Satisfaction Analysis";
        var text  = msgs.LastOrDefault()?.Text ?? "(no output)";
        return $"{label}\n\n{text}";
    });

    var combined = string.Join("\n\n---\n\n", sections);
    return [new ChatMessage(ChatRole.Assistant,
        $"# After-Sale Admin Report\n\n{combined}")];
}
```

`results` is an `IReadOnlyList<IReadOnlyList<ChatMessage>>` — one message list per concurrent agent, in declaration order.

---

## Seeded Data

### Order Summary (10 orders)

| Status    | Count |
|-----------|-------|
| Delivered | 7     |
| Shipped   | 2     |
| Pending   | 1     |
| Cancelled | 1     |

### Customer Satisfaction (5 customers)

| Customer       | Tier     | Score | At Risk? |
|----------------|----------|-------|----------|
| Alice Johnson  | Premium  | 5     |          |
| Bob Smith      | Standard | 3     |          |
| Carol White    | VIP      | 4     |          |
| David Lee      | Standard | 2     | ⚠ Yes   |
| Eva Martinez   | Premium  | 5     |          |

---

## Endpoint

```http
POST /agents/after-sale-report
Content-Type: application/json

{ "input": "Give me the monthly sales and satisfaction report" }
```

**Response structure**

```markdown
# After-Sale Admin Report

## Sales Analysis

- **Total Orders**: 10
- **Total Revenue**: $12,949.90 USD
- **Order Status Breakdown**:
  - Delivered: 7
  - Shipped: 2
  - Pending: 1
  - Cancelled: 1 ⚠ (10% cancellation rate — monitor for trends)
- **Top Products by Revenue**:
  1. MacBook Pro 14 M4 — 3 units — $5,999.97
  2. Dell XPS 15 (2025) — 2 units — $3,599.98
  ...

---

## Customer Satisfaction Analysis

- **Average CSAT Score**: 3.8 / 5
- **NPS Category**: Good
- **Score Distribution**: 5★×2, 4★×1, 3★×1, 2★×1
- **Customers by Tier**: Premium×2, Standard×2, VIP×1
- **⚠ At-Risk Customers (immediate follow-up required)**:
  - David Lee (CUST-004) — Score: 2 — recent delivery issue
```

---

## API Reference

| Property                | Value                                  |
|-------------------------|----------------------------------------|
| Route                   | `POST /agents/after-sale-report`       |
| Registered name (DI)    | `"AfterSaleReportWorkflowAgent"`       |
| Workflow builder        | `AgentWorkflowBuilder.BuildConcurrent` |
| Execution mode          | `InProcessExecution.OffThread`         |
| Aggregator              | C# delegate (no extra LLM call)        |

---

## When to Use the Concurrent Pattern

| Scenario | Concurrent ✓ | Sequential | Handoff |
|----------|-------------|------------|---------|
| Independent sub-tasks that can run in parallel | ✓ | — | — |
| Results must be merged into one output | ✓ | — | — |
| Steps depend on the previous step's output | — | ✓ | — |
| Routing between specialists based on intent | — | — | ✓ |
| Admin dashboards / report generation | ✓ | — | — |

---

## Related

- [Sales Workflow — Sequential Pattern](sales-workflow.md)
- [Customer Service Workflow — Handoff Pattern](customer-service-workflow.md)
- [Main README](../README.md)
