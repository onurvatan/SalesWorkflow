# Sample Prompts & Expected Workflow Routing

This document provides example user prompts and explains which agent the
`OrchestratorAgent` should route them to, along with the reasoning behind each
routing decision.

---

## How Routing Works

When a request is sent to `POST /agents`, the `OrchestratorGroupChatManager`
builds a short roster of available agents — each with its name and description —
and asks the LLM: _"Which agent should handle this request?"_

The LLM responds with an agent name. The manager matches it
case-insensitively against the participant list and delegates accordingly. If
no match is found, it falls back to the first participant
(`CustomerServiceWorkflowAgent`).

---

## SalesWorkflowAgent

**Handles:** product discovery, catalog search, stock checks, and recommendations.  
**Registered only when** `AzureSearch` settings are configured in `appsettings.json`.

| Prompt                                        | Routing Reason                                         |
| --------------------------------------------- | ------------------------------------------------------ |
| `"I want to buy a Dell laptop"`               | Clear purchase intent with a specific product category |
| `"Show me wireless headphones under $100"`    | Catalog browse with a price constraint                 |
| `"Do you have any gaming monitors in stock?"` | Stock check combined with product search               |
| `"What's the best keyboard for programming?"` | Product recommendation request                         |
| `"Compare MacBook Pro vs Surface Laptop"`     | Multi-product catalog lookup                           |
| `"Are there any standing desks available?"`   | Inventory / catalog question                           |

**What happens inside:**

1. `CatalogSearchTool` queries Azure AI Search for matching products.
2. `StockCheckTool` verifies availability for the matched SKUs.
3. The agent returns a ranked list of products with stock status.

---

## CustomerServiceWorkflowAgent

**Handles:** order status, billing disputes, shipping issues, returns, and escalations.  
**Pattern:** Handoff — a Triage agent classifies the issue and hands off to either
a Billing Specialist or a Shipping Specialist.

| Prompt                                             | Routing Reason                         |
| -------------------------------------------------- | -------------------------------------- |
| `"I want a refund for my order ORD-004"`           | Explicit refund/billing intent         |
| `"My package hasn't arrived yet, order ORD-007"`   | Shipping / delivery issue              |
| `"Why was I charged twice for my order?"`          | Billing dispute; may escalate to human |
| `"What's the status of my order ORD-002?"`         | Order lookup via `order_status` tool   |
| `"I received the wrong item, how do I return it?"` | Post-purchase support                  |
| `"My delivery is late, can you check CUST-003?"`   | Shipping specialist path               |

**What happens inside:**

1. **Triage agent** uses `customer_lookup` and `order_status` tools to identify the
   customer and classify the issue (billing vs. shipping).
2. **Handoff** — control passes to the appropriate specialist agent.
3. **Billing Specialist** resolves charge disputes or calls `escalate_to_human` when
   manual intervention is needed.
4. **Shipping Specialist** tracks delivery and advises on next steps.

---

## AfterSaleReportWorkflowAgent

**Handles:** administration and analytics — sales summaries, customer satisfaction
scores, and churn-risk reports.  
**Pattern:** Concurrent — both reporting tools run in parallel to reduce latency.

| Prompt                                                 | Routing Reason                                |
| ------------------------------------------------------ | --------------------------------------------- |
| `"Show me the sales report for this month"`            | Explicit sales analytics request              |
| `"What is our customer satisfaction score?"`           | CSAT reporting intent                         |
| `"Give me the monthly sales and satisfaction summary"` | Combined report — both tools run concurrently |
| `"Which products sold the most last quarter?"`         | Sales performance analytics                   |
| `"How many at-risk customers do we have?"`             | CSAT / churn-risk query                       |
| `"Generate an after-sales performance dashboard"`      | Broad reporting intent                        |

**What happens inside:**

1. `SalesReportTool` and `CustomerSatisfactionTool` execute **concurrently**.
2. Results are merged into a single structured JSON response.
3. The response includes `totalOrders`, `topProducts`, `averageScore`, and
   `atRiskCustomers`.

---

## Ambiguous / Off-Topic Prompts

When the LLM cannot identify a clear intent, or returns an agent name that
does not match any participant, the orchestrator falls back to the **first
registered participant** (`CustomerServiceWorkflowAgent`).

| Prompt                            | Behaviour                         |
| --------------------------------- | --------------------------------- |
| `"Help me"`                       | No recognisable intent → fallback |
| `"Tell me something interesting"` | Off-topic → fallback              |
| `"What are your business hours?"` | General enquiry → fallback        |

---

## Catalog Not Configured

If `AzureSearch` settings are absent or incomplete, `SalesWorkflowAgent` is **not
registered**. In that case the orchestrator only has two participants:

```
1. CustomerServiceWorkflowAgent
2. AfterSaleReportWorkflowAgent
```

Any product-related prompt (e.g. `"I want to buy a Dell laptop"`) will fail to
match and fall back to `CustomerServiceWorkflowAgent`.

---

## Quick Reference

```
POST /agents  { "input": "<your prompt>" }

Product / catalog query      → SalesWorkflowAgent           (Sequential pattern)
Order / billing / shipping   → CustomerServiceWorkflowAgent  (Handoff pattern)
Reports / analytics          → AfterSaleReportWorkflowAgent  (Concurrent pattern)
Unrecognised intent          → CustomerServiceWorkflowAgent  (fallback)
```

---

## Console Logs

The application emits structured logs at runtime so you can observe routing
decisions without a debugger. Log levels are configured in
`appsettings.Development.json`.

### SalesWorkflowAgent — `POST /agents/sales-workflow`

```
info: Program[0]
      [SalesWorkflowAgent] Received: I want to buy a Dell laptop
info: Program[0]
      [SalesWorkflowAgent] Completed.
```

### CustomerServiceWorkflowAgent — `POST /agents/customer-service`

```
info: Program[0]
      [CustomerServiceWorkflowAgent] Received: I want a refund for my order ORD-004
info: Program[0]
      [CustomerServiceWorkflowAgent] Completed.
```

### AfterSaleReportWorkflowAgent — `POST /agents/after-sale-report`

```
info: Program[0]
      [AfterSaleReportWorkflowAgent] Received: Show me the sales report for this month
info: Program[0]
      [AfterSaleReportWorkflowAgent] Completed.
```

### OrchestratorAgent — `POST /agents` (normal routing)

```
info: Program[0]
      [OrchestratorAgent] Received: I want to buy a Dell laptop
dbug: SalesWorkflow.Agents.OrchestratorGroupChatManager[0]
      Orchestrator LLM routing response: 'SalesWorkflowAgent'
info: SalesWorkflow.Agents.OrchestratorGroupChatManager[0]
      [Orchestrator] Routing decision (turn 1/3): 'SalesWorkflowAgent'
info: SalesWorkflow.Agents.OrchestratorGroupChatManager[0]
      [Orchestrator] Terminating — [DONE] sentinel detected in last assistant message.
info: Program[0]
      [OrchestratorAgent] Completed.
```

### OrchestratorAgent — `POST /agents` (fallback routing)

Triggered when the LLM returns a name that does not match any participant
(e.g. prompt `"Help me"`):

```
info: Program[0]
      [OrchestratorAgent] Received: Help me
dbug: SalesWorkflow.Agents.OrchestratorGroupChatManager[0]
      Orchestrator LLM routing response: 'UnknownAgent'
info: SalesWorkflow.Agents.OrchestratorGroupChatManager[0]
      [Orchestrator] Routing decision (turn 1/3): 'CustomerServiceWorkflowAgent' [fallback — LLM returned unrecognised name]
info: Program[0]
      [OrchestratorAgent] Completed.
```

### OrchestratorAgent — `POST /agents` (max iterations reached)

```
info: SalesWorkflow.Agents.OrchestratorGroupChatManager[0]
      [Orchestrator] Terminating — max iterations (3) reached.
```

### Log Level Reference

| Category                                            | Level         | What it shows                                     |
| --------------------------------------------------- | ------------- | ------------------------------------------------- |
| `Default`                                           | `Information` | Endpoint received / completed + routing decisions |
| `SalesWorkflow.Agents.OrchestratorGroupChatManager` | `Debug`       | Raw LLM routing response string                   |
| `Microsoft.Agents`                                  | `Information` | Framework-level agent events                      |
| `Microsoft.AspNetCore`                              | `Warning`     | HTTP pipeline noise suppressed                    |

> **Tip:** To promote LLM routing responses to `Information` in production, change
> `SalesWorkflow.Agents.OrchestratorGroupChatManager` to `Information` in
> `appsettings.json`.

---

## Related Docs

- [Orchestrator Workflow](orchestrator-workflow.md)
- [Sales Workflow](sales-workflow.md)
- [Customer Service Workflow](customer-service-workflow.md)
- [After-Sale Report Workflow](after-sale-report-workflow.md)
