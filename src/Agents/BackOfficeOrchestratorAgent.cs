#pragma warning disable MAAIW001 // Experimental Microsoft.Agents.AI.Workflows APIs
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace SalesWorkflow.Agents;

/// <summary>
/// Admin-only orchestrator workflow using the <b>GroupChat</b> pattern.
/// <para>
/// A single <c>POST /admin/agents</c> entry point accepts back-office prompts.
/// The <see cref="OrchestratorGroupChatManager"/> uses the LLM to classify intent and
/// delegates to the appropriate admin workflow agent:
/// </para>
/// <list type="bullet">
///   <item><b>AfterSaleReportWorkflowAgent</b> — admin sales/CSAT reports</item>
/// </list>
/// </summary>
public class BackOfficeOrchestratorAgent(IChatClient chatClient)
{
    public const string AgentName = "BackOfficeOrchestratorAgent";

    public const string WorkflowDescription =
        "Admin GroupChat orchestrator: routes back-office prompts to AfterSaleReportWorkflow.";

    public AIAgent CreateAgent(
        string name,
        IReadOnlyList<AIAgent> participants,
        ILogger<OrchestratorGroupChatManager>? logger = null)
    {
        var workflow = AgentWorkflowBuilder
            .CreateGroupChatBuilderWith(p => new OrchestratorGroupChatManager(chatClient, p, logger))
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
