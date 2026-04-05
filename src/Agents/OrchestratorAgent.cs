#pragma warning disable MAAIW001 // Experimental Microsoft.Agents.AI.Workflows APIs
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace SalesWorkflow.Agents;

/// <summary>
/// LLM-driven <see cref="GroupChatManager"/> that selects the next workflow agent
/// based on the user's intent, inferred by the chat model at each turn.
/// </summary>
/// <remarks>
/// <para>
/// <b>Selection logic</b>: the manager sends a compact system prompt to the LLM listing
/// each participant's name and description, then asks the model to respond with exactly
/// one agent name. The response is matched case-insensitively against participants.
/// </para>
/// <para>
/// <b>Termination</b>: the workflow stops when <see cref="GroupChatManager.IterationCount"/>
/// reaches <see cref="GroupChatManager.MaximumIterationCount"/> (default 3), or when the
/// most recent assistant message contains the sentinel token <c>[DONE]</c>.
/// </para>
/// <para>
/// <b>Fallback</b>: if the LLM returns an unrecognised name the manager defaults to the
/// first participant in the list.
/// </para>
/// </remarks>
public class OrchestratorGroupChatManager : GroupChatManager
{
    private readonly IChatClient _chatClient;
    private readonly IReadOnlyList<AIAgent> _participants;

    public OrchestratorGroupChatManager(IChatClient chatClient, IReadOnlyList<AIAgent> participants)
    {
        _chatClient = chatClient;
        _participants = participants;
        MaximumIterationCount = 3;
    }

    /// <summary>
    /// Asks the LLM which participant should handle the conversation next.
    /// </summary>
    protected override async ValueTask<AIAgent> SelectNextAgentAsync(
        IReadOnlyList<ChatMessage> history,
        CancellationToken cancellationToken)
    {
        // Build a roster: "AgentName — description" per line
        var roster = string.Join("\n", _participants.Select(p =>
            $"- {p.Name}: {p.Description}"));

        // Take the most recent user turn as the intent signal
        var userIntent = history.LastOrDefault(m => m.Role == ChatRole.User)?.Text
                         ?? history.LastOrDefault()?.Text
                         ?? string.Empty;

        var systemPrompt =
            "You are a routing orchestrator. Given the following user request and the list of " +
            "available workflow agents, respond with ONLY the exact agent name (no explanation, " +
            "no punctuation) that should handle this request.\n\n" +
            $"Available agents:\n{roster}\n\n" +
            $"User request: {userIntent}";

        var response = await _chatClient.GetResponseAsync(
            [new ChatMessage(ChatRole.User, systemPrompt)],
            cancellationToken: cancellationToken);

        var chosen = response.Text?.Trim() ?? string.Empty;

        // Match by name (case-insensitive, allow partial e.g. "CustomerService")
        var match = _participants.FirstOrDefault(p =>
            string.Equals(p.Name, chosen, StringComparison.OrdinalIgnoreCase)
            || (p.Name?.Contains(chosen, StringComparison.OrdinalIgnoreCase) ?? false)
            || (chosen.Contains(p.Name ?? string.Empty, StringComparison.OrdinalIgnoreCase)));

        // Fallback to first participant if nothing matched
        return match ?? _participants[0];
    }

    /// <summary>
    /// Terminates after <see cref="GroupChatManager.MaximumIterationCount"/> turns,
    /// or earlier if the last assistant message contains the <c>[DONE]</c> sentinel.
    /// </summary>
    protected override ValueTask<bool> ShouldTerminateAsync(
        IReadOnlyList<ChatMessage> history,
        CancellationToken cancellationToken)
    {
        if (IterationCount >= MaximumIterationCount)
            return ValueTask.FromResult(true);

        var lastAssistant = history.LastOrDefault(m => m.Role == ChatRole.Assistant);
        if (lastAssistant?.Text?.Contains("[DONE]", StringComparison.OrdinalIgnoreCase) == true)
            return ValueTask.FromResult(true);

        return ValueTask.FromResult(false);
    }

    /// <summary>Pass-through — returns the history unchanged.</summary>
    protected override ValueTask<IEnumerable<ChatMessage>> UpdateHistoryAsync(
        IReadOnlyList<ChatMessage> history,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IEnumerable<ChatMessage>>(history);
}

/// <summary>
/// Orchestrator workflow using the <b>GroupChat</b> pattern.
/// <para>
/// A single <c>POST /agents</c> entry point accepts any prompt. The
/// <see cref="OrchestratorGroupChatManager"/> uses the LLM to classify intent and
/// delegates to the appropriate workflow agent as the next speaker:
/// </para>
/// <list type="bullet">
///   <item><b>CustomerServiceWorkflowAgent</b> — billing disputes, shipping issues, order status</item>
///   <item><b>AfterSaleReportWorkflowAgent</b> — admin sales/CSAT reports</item>
///   <item><b>SalesWorkflowAgent</b> — product search (registered only when catalog is configured)</item>
/// </list>
/// </summary>
public class OrchestratorAgent(IChatClient chatClient)
{
    public const string AgentName = "OrchestratorAgent";

    public const string WorkflowDescription =
        "GroupChat orchestrator: routes prompts to CustomerServiceWorkflow | AfterSaleReportWorkflow | SalesWorkflow.";

    public AIAgent CreateAgent(string name, IReadOnlyList<AIAgent> participants)
    {
        var workflow = AgentWorkflowBuilder
            .CreateGroupChatBuilderWith(p => new OrchestratorGroupChatManager(chatClient, p))
            .AddParticipants(participants)
            .WithName(name)
            .WithDescription(WorkflowDescription)
            .Build();

        return Create(workflow, name);
    }

    public static AIAgent Create(Workflow workflow, string name) =>
        workflow.AsAIAgent(
            name,
            name,
            WorkflowDescription,
            InProcessExecution.OffThread,
            includeExceptionDetails: false,
            includeWorkflowOutputsInResponse: false);
}
