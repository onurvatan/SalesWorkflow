# Orchestrator Workflow — GroupChat Pattern

`POST /agents`

The `ClientOrchestratorAgent` demonstrates the **GroupChat** workflow pattern from [Microsoft.Agents.AI.Workflows](https://www.nuget.org/packages/Microsoft.Agents.AI.Workflows). A single entry point accepts any prompt. A custom `OrchestratorGroupChatManager` uses the LLM to classify the user's intent and selects the appropriate workflow agent as the next speaker — without hardcoded routing rules.

---

## Topology

```
POST /agents  { "input": "..." }
      │
      ▼
┌─────────────────────────────────────────────────────────────────────┐
│  ClientOrchestratorAgent  (GroupChat workflow)                      │
│                                                                     │
│  OrchestratorGroupChatManager                                       │
│    SelectNextAgentAsync()  ── LLM classifies intent                 │
│    ShouldTerminateAsync()  ── stops after ≤ MaximumIterationCount   │
│    MaximumIterationCount   = 3  (safety ceiling)                    │
│                       │                                             │
│      ┌────────────────┼────────────────┐                            │
│      ▼                ▼                                           │
│  CustomerService  SalesWorkflow                                     │
│  WorkflowAgent    Agent                                                │
│  (Handoff)        (Sequential)                                        │
│                   ⚠ only when catalog configured                     │
└─────────────────────────────────────────────────────────────────────┘
      │
      ▼
Final response from selected workflow agent
```

Participants are the **already-registered keyed `AIAgent` singletons** pulled from DI at factory time. No new tools are needed — the orchestrator purely composes what already exists.

---

## Routing Logic

### `SelectNextAgentAsync`

At each turn the manager sends this prompt to the LLM:

```
You are a routing orchestrator. Given the following user request and the list of
available workflow agents, respond with ONLY the exact agent name (no explanation,
no punctuation) that should handle this request.

Available agents:
- CustomerServiceWorkflowAgent: Handoff customer-service workflow: triage-agent → billing-specialist | shipping-specialist.
- SalesWorkflowAgent:           Sequential sales workflow: catalog-retriever → stock-checker → sales-responder.

User request: <last user message>
```

The response is matched case-insensitively against participant names. Partial matches are accepted (`"CustomerService"` → `CustomerServiceWorkflowAgent`). If no match is found the manager falls back to the **first participant** (`CustomerServiceWorkflowAgent`).

### `ShouldTerminateAsync`

The workflow terminates when either condition is met:

| Condition                                 | Detail                                               |
| ----------------------------------------- | ---------------------------------------------------- |
| `IterationCount >= MaximumIterationCount` | Safety ceiling (default: 3) — prevents runaway loops |
| Last assistant message contains `[DONE]`  | Participant signals completion (optional convention) |

### Routing examples

| User prompt                                | Selected agent                 |
| ------------------------------------------ | ------------------------------ |
| `"I want a refund for ORD-004"`            | `CustomerServiceWorkflowAgent` |
| `"My order ORD-002 hasn't arrived"`        | `CustomerServiceWorkflowAgent` |
| `"Which laptops do you have under $1500?"` | `SalesWorkflowAgent`           |
| `"Which laptops do you have under $1500?"` | `SalesWorkflowAgent`           |
| `"I want a refund for ORD-004"`            | `CustomerServiceWorkflowAgent` |

---

## `OrchestratorGroupChatManager` API

Custom subclass of `GroupChatManager` (abstract base from the framework):

```csharp
public sealed class OrchestratorGroupChatManager : GroupChatManager
{
    // ctor(IChatClient chatClient, IReadOnlyList<AIAgent> participants)
    //   MaximumIterationCount = 3

    // LLM-based selection
    public override ValueTask<AIAgent> SelectNextAgentAsync(
        IReadOnlyList<ChatMessage> history, CancellationToken ct);

    // Terminates on ceiling or [DONE] sentinel
    public override ValueTask<bool> ShouldTerminateAsync(
        IReadOnlyList<ChatMessage> history, CancellationToken ct);

    // No-op pass-through
    public override ValueTask UpdateHistoryAsync(
        IReadOnlyList<ChatMessage> history, CancellationToken ct);
}
```

**Why a custom manager instead of `RoundRobinGroupChatManager`?**  
`RoundRobinGroupChatManager` cycles through participants in fixed order — it has no awareness of intent. For routing, we need `SelectNextAgentAsync` to make an LLM call and choose the right domain expert.

---

## Participants & Conditional Registration

| Participant                    | Always registered? | Condition                                             |
| ------------------------------ | ------------------ | ----------------------------------------------------- |
| `CustomerServiceWorkflowAgent` | Yes                | Always                                                |
| `SalesWorkflowAgent`           | No                 | Only when `SalesIndex:CatalogIndexName` is configured |

Note: `AfterSaleReportWorkflowAgent` is registered by the app (used by admin/back-office flows) but is intentionally NOT a participant of the `ClientOrchestratorAgent`.

The orchestrator's DI factory captures `hasCatalog` at registration time:

```csharp
builder.AddAIAgent(ClientOrchestratorAgent.AgentName, (sp, name) =>
{
    var participants = new List<AIAgent>
    {
        sp.GetRequiredKeyedService<AIAgent>(CustomerServiceWorkflowAgent.AgentName),
    };
    if (hasCatalog)
        participants.Add(sp.GetRequiredKeyedService<AIAgent>(SalesWorkflowAgent.AgentName));

    return sp.GetRequiredService<ClientOrchestratorAgent>().CreateAgent(name, participants);
}, ServiceLifetime.Singleton);
```

---

## Endpoint

```http
POST /agents
Content-Type: application/json

{ "input": "Show me last month's revenue and flag any at-risk customers" }
```

**Response**

```json
{
  "agentName": "ClientOrchestratorAgent",
  "result": "# After-Sale Admin Report\n\n## Sales Analysis\n\n- **Total Orders**: 10...\n\n---\n\n## Customer Satisfaction Analysis\n\n- ⚠ At-Risk: David Lee (CUST-004) — Score: 2..."
}
```

**Customer service example**

```http
POST /agents
Content-Type: application/json

{ "input": "I need a refund for order ORD-004 — it was cancelled but I was charged" }
```

The orchestrator routes to `CustomerServiceWorkflowAgent` → triage-agent classifies as billing → billing-specialist handles the refund/escalation.

---

## API Reference

| Property                | Value                                               |
| ----------------------- | --------------------------------------------------- |
| Route                   | `POST /agents`                                      |
| Registered name (DI)    | `"ClientOrchestratorAgent"`                         |
| Workflow builder        | `AgentWorkflowBuilder.CreateGroupChatBuilderWith`   |
| Manager                 | `OrchestratorGroupChatManager` (custom, LLM-driven) |
| Execution mode          | `InProcessExecution.OffThread`                      |
| `MaximumIterationCount` | 3                                                   |
| Experimental API pragma | `#pragma warning disable MAAIW001`                  |

---

## When to Use Each Pattern

|                                           | Sequential | Handoff | Concurrent | **GroupChat** |
| ----------------------------------------- | ---------- | ------- | ---------- | ------------- |
| Steps depend on previous output           | ✓          | —       | —          | —             |
| Route to specialist by intent             | —          | ✓       | —          | ✓             |
| Independent sub-tasks in parallel         | —          | —       | ✓          | —             |
| **Single entry point, unknown intent**    | —          | —       | —          | **✓**         |
| Admin dashboard / report generation       | —          | —       | ✓          | ✓             |
| Customer-facing UX with no routing burden | —          | —       | —          | ✓             |

---

## Related

- [Sales Workflow — Sequential Pattern](sales-workflow.md)
- [Customer Service Workflow — Handoff Pattern](customer-service-workflow.md)
- [After-Sale Report Workflow — Concurrent Pattern](after-sale-report-workflow.md)
- [Main README](../README.md)
