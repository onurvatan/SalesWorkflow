#pragma warning disable MAAIW001 // Experimental Microsoft.Agents.AI.Workflows APIs
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using SalesWorkflow.Data;
using SalesWorkflow.Tools;

namespace SalesWorkflow.Agents;

/// <summary>
/// After-sale admin report workflow using the <b>Concurrent</b> (fan-out / fan-in) pattern.
/// <para>
/// Topology: sales-analyst ─┐
///                           ├─► aggregator → combined admin report
///   satisfaction-analyst ──┘
/// </para>
/// Both analysts execute in parallel; their outputs are merged into a single report
/// by the aggregator delegate.
/// </summary>
public class AfterSaleReportWorkflowAgent(
    IChatClient chatClient,
    IOrderRepository orderRepo,
    ICustomerRepository customerRepo)
{
    public const string AgentName = "AfterSaleReportWorkflowAgent";

    public const string WorkflowDescription =
        "Concurrent after-sale report workflow: sales-analyst ‖ satisfaction-analyst → merged admin report.";

    public const string SalesAnalystInstructions =
        "You are a sales analyst. Use the sales_report tool to retrieve current revenue data. " +
        "Summarise: total orders, total revenue, status breakdown (Pending/Shipped/Delivered/Cancelled), " +
        "and the top-selling products by revenue. Flag any anomalies such as high cancellation rates. " +
        "Present findings clearly for an admin dashboard. Use bullet points and currency formatting.";

    public const string SatisfactionAnalystInstructions =
        "You are a customer satisfaction analyst. Use the customer_satisfaction_report tool to retrieve CSAT data. " +
        "Summarise: average satisfaction score, score distribution, customers by tier, " +
        "and the list of at-risk customers (score ≤ 2) who need immediate follow-up. " +
        "Classify the overall NPS category. Present findings clearly for an admin dashboard. " +
        "Use bullet points and highlight at-risk customers prominently.";

    public AIAgent CreateAgent(string name)
    {
        var salesAnalyst = chatClient.AsAIAgent(
            instructions: SalesAnalystInstructions,
            name: "sales-analyst",
            tools: [SalesReportTool.Create(orderRepo)]);

        var satisfactionAnalyst = chatClient.AsAIAgent(
            instructions: SatisfactionAnalystInstructions,
            name: "satisfaction-analyst",
            tools: [CustomerSatisfactionTool.Create(customerRepo)]);

        // Concurrent workflow: both analysts run in parallel, aggregator merges their outputs
        var workflow = AgentWorkflowBuilder.BuildConcurrent(
            name,
            [salesAnalyst, satisfactionAnalyst],
            aggregator: results =>
            {
                var sections = results
                    .Select((msgs, i) =>
                    {
                        var label = i == 0 ? "## Sales Analysis" : "## Customer Satisfaction Analysis";
                        var text = msgs.LastOrDefault()?.Text ?? "(no output)";
                        return $"{label}\n\n{text}";
                    });

                var combined = string.Join("\n\n---\n\n", sections);
                return [new ChatMessage(ChatRole.Assistant,
                    $"# After-Sale Admin Report\n\n{combined}")];
            });

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
