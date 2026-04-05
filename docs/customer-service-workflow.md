# Customer Service Workflow — Handoff Pattern

`POST /agents/customer-service`

The `CustomerServiceWorkflowAgent` demonstrates the **Handoff** workflow pattern from [Microsoft.Agents.AI.Workflows](https://www.nuget.org/packages/Microsoft.Agents.AI.Workflows). A triage agent classifies the customer's intent and **hands off** control to whichever specialist can resolve the issue. Specialists can route back to triage if they receive a misclassified request (`EnableReturnToPrevious`).

---

## Topology

```
Customer message
      │
      ▼
┌─────────────────────────────────────────────────────┐
│  triage-agent                                       │
│  tools: customer_lookup, order_status               │
│  → classifies intent (billing vs. shipping)         │
└──────────────┬──────────────────────────────────────┘
               │  handoff
       ┌───────┴────────┐
       ▼                ▼
┌─────────────┐  ┌──────────────────┐
│  billing-   │  │  shipping-       │
│  specialist │  │  specialist      │
│             │  │                  │
│  order_status  │  order_status    │
│  escalate_  │  │                  │
│  to_human   │  │                  │
└──────┬──────┘  └────────┬─────────┘
       │                  │
       └──── return ───► triage (if misclassified)
```

**`EnableReturnToPrevious()`** — if a specialist determines the request belongs to the other domain, it hands control back to `triage-agent`, which can re-route.

---

## Agents

### `triage-agent`

| Property         | Value                                        |
| ---------------- | -------------------------------------------- |
| Tools            | `customer_lookup`, `order_status`            |
| Responsibility   | Classify intent; route to correct specialist |
| Hand-off targets | `billing-specialist`, `shipping-specialist`  |

**Instructions (excerpt)**

> Determine the correct specialist: hand off to `billing-specialist` for refund requests, payment disputes, or billing questions; hand off to `shipping-specialist` for delivery tracking, shipment delays, or address issues. If the intent is unclear, ask one clarifying question before handing off.

---

### `billing-specialist`

| Property       | Value                                               |
| -------------- | --------------------------------------------------- |
| Tools          | `order_status`, `escalate_to_human`                 |
| Responsibility | Refunds, billing disputes, payment questions        |
| Escalation     | Automatic when refund > $1 000 or repeated failures |

**Instructions (excerpt)**

> If the refund value exceeds $1000 or involves repeated failures, use the `escalate_to_human` tool and inform the customer of the escalation request ID and expected response time.

---

### `shipping-specialist`

| Property       | Value                                                         |
| -------------- | ------------------------------------------------------------- |
| Tools          | `order_status`                                                |
| Responsibility | Tracking, delivery delays, address corrections, lost packages |

**Instructions (excerpt)**

> Provide specific estimated delivery information based on order status (`Pending`, `Shipped`, `Delivered`).

---

## Tools

### `customer_lookup`

```
Input : query (string) — Customer ID (e.g. CUST-001) or partial name
Output: JSON — customer profile + last 5 orders
```

Fields returned: `customerId`, `name`, `email`, `tier`, `totalOrders`, `totalSpent`, `currency`, `satisfactionScore`, `notes`, `recentOrders[]`

### `order_status`

```
Input : query (string) — Order ID (e.g. ORD-003) or Customer ID (e.g. CUST-002)
Output: JSON array — order details for matched record(s)
```

Fields returned: `orderId`, `customerId`, `productSku`, `productName`, `quantity`, `totalAmount`, `currency`, `orderDate`, `status`

**Order statuses**: `Pending` · `Shipped` · `Delivered` · `Cancelled`

### `escalate_to_human`

```
Input : reason (string) — brief escalation reason
Output: JSON — escalation record with requestId and ETA message
```

```json
{
  "escalated": true,
  "requestId": "ESC-A3B4C5D6",
  "reason": "Refund request over $1000",
  "message": "This case has been escalated to a human agent (request ESC-A3B4C5D6). A support specialist will contact the customer within 1 business day.",
  "timestamp": "2026-04-05T10:30:00Z"
}
```

> **HITL extension point** — in production, replace the stub with an `ExternalRequest` on a `RequestPort` to suspend the workflow and await `StreamingRun.SendResponseAsync` from the human agent before continuing.

---

## Seeded Data

### Customers (5)

| ID       | Name          | Tier     | Satisfaction (1–5) | Notes                           |
| -------- | ------------- | -------- | ------------------ | ------------------------------- |
| CUST-001 | Alice Johnson | Premium  | 5                  | Long-standing customer          |
| CUST-002 | Bob Smith     | Standard | 3                  | Recent billing dispute          |
| CUST-003 | Carol White   | VIP      | 4                  | Prefers email contact           |
| CUST-004 | David Lee     | Standard | 2                  | At-risk — recent delivery issue |
| CUST-005 | Eva Martinez  | Premium  | 5                  | Loyal customer, frequent buyer  |

### Orders (10)

| Order ID | Customer | Product                  | Status    | Total     |
| -------- | -------- | ------------------------ | --------- | --------- |
| ORD-001  | CUST-001 | Dell XPS 15 (2025)       | Delivered | $1,799.99 |
| ORD-002  | CUST-001 | Apple AirPods Pro 3      | Shipped   | $249.99   |
| ORD-003  | CUST-002 | MacBook Pro 14 M4        | Delivered | $1,999.99 |
| ORD-004  | CUST-002 | Samsung Galaxy S25 Ultra | Cancelled | $1,299.99 |
| ORD-005  | CUST-003 | Sony WH-1000XM6          | Delivered | $399.99   |
| ORD-006  | CUST-003 | Apple MacBook Pro 14 M4  | Shipped   | $1,999.99 |
| ORD-007  | CUST-004 | Logitech MX Master 4     | Pending   | $99.99    |
| ORD-008  | CUST-004 | Dell XPS 15 (2025)       | Delivered | $1,799.99 |
| ORD-009  | CUST-005 | Apple MacBook Pro 14 M4  | Delivered | $1,999.99 |
| ORD-010  | CUST-005 | Samsung Galaxy S25 Ultra | Delivered | $1,299.99 |

---

## Endpoint

```http
POST /agents/customer-service
Content-Type: application/json

{ "input": "My order ORD-002 hasn't arrived yet" }
```

**Response**

```json
{
  "agentName": "CustomerServiceWorkflowAgent",
  "result": "I've looked up your order ORD-002 (Apple AirPods Pro 3, $249.99). It is currently **Shipped** and on its way. Standard delivery takes 3–5 business days from the ship date. If it hasn't arrived within that window, please contact us again and I can open a trace request."
}
```

**Billing escalation example**

```http
POST /agents/customer-service
Content-Type: application/json

{ "input": "I want a refund for ORD-004 — the phone was cancelled but I was still charged $1299" }
```

The triage agent identifies a billing issue → hands off to `billing-specialist` → specialist raises an escalation (refund > $1 000) and returns `requestId`.

---

## API Reference

| Property                | Value                                           |
| ----------------------- | ----------------------------------------------- |
| Route                   | `POST /agents/customer-service`                 |
| Registered name (DI)    | `"CustomerServiceWorkflowAgent"`                |
| Workflow builder        | `AgentWorkflowBuilder.CreateHandoffBuilderWith` |
| Execution mode          | `InProcessExecution.OffThread`                  |
| Experimental API pragma | `#pragma warning disable MAAIW001`              |

---

## When to Use the Handoff Pattern

| Scenario                                      | Handoff ✓ | Sequential |
| --------------------------------------------- | --------- | ---------- |
| Different specialists handle distinct domains | ✓         | —          |
| Triage step required before routing           | ✓         | —          |
| Strict step-by-step pipeline                  | —         | ✓          |
| Complex back-and-forth with human escalation  | ✓         | —          |

---

## Related

- [Sales Workflow — Sequential Pattern](sales-workflow.md)
- [After-Sale Report Workflow — Concurrent Pattern](after-sale-report-workflow.md)
- [Main README](../README.md)
