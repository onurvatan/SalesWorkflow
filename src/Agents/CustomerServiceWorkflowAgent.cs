#pragma warning disable MAAIW001 // Experimental Microsoft.Agents.AI.Workflows APIs
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using SalesWorkflow.Data;
using SalesWorkflow.Tools;

namespace SalesWorkflow.Agents;

/// <summary>
/// Customer service workflow using the <b>Handoff</b> pattern.
/// <para>
/// Topology: triage-agent (initial) → billing-specialist | shipping-specialist
/// </para>
/// <list type="bullet">
///   <item><b>triage-agent</b> — classifies intent using customer_lookup + order_status,
///     then hands off to the right specialist.</item>
///   <item><b>billing-specialist</b> — handles refunds and billing disputes; can escalate
///     to a human via escalate_to_human (HITL simulation).</item>
///   <item><b>shipping-specialist</b> — handles delivery tracking and shipping issues.</item>
/// </list>
/// EnableReturnToPrevious() lets a specialist route back to triage if misclassified.
/// </summary>
public class CustomerServiceWorkflowAgent(
    IChatClient chatClient,
    ICustomerRepository customerRepo,
    IOrderRepository orderRepo)
{
    public const string AgentName = "CustomerServiceWorkflowAgent";

    public const string WorkflowDescription =
        "Handoff customer-service workflow: triage-agent → billing-specialist | shipping-specialist.";

    public const string TriageInstructions =
        "You are a customer service triage agent. Use the customer_lookup tool to find the customer " +
        "and the order_status tool to understand their issue. " +
        "Then determine the correct specialist: " +
        "hand off to billing-specialist for refund requests, payment disputes, or billing questions; " +
        "hand off to shipping-specialist for delivery tracking, shipment delays, or address issues. " +
        "If the intent is unclear, ask one clarifying question before handing off.";

    public const string BillingSpecialistInstructions =
        "You are a billing specialist. Use the order_status tool to review the customer's order details. " +
        "Help with refund requests, billing disputes, and payment questions. " +
        "If the refund value exceeds $1000 or involves repeated failures, use the escalate_to_human tool " +
        "and inform the customer of the escalation request ID and expected response time.";

    public const string ShippingSpecialistInstructions =
        "You are a shipping specialist. Use the order_status tool to check current delivery status. " +
        "Help with shipment tracking, delivery delays, wrong address corrections, and lost package claims. " +
        "Provide specific estimated delivery information based on order status (Pending, Shipped, Delivered).";

    public AIAgent CreateAgent(string name, bool runningAsGroupChatParticipant = false)
    {
        var triageAgent = chatClient.AsAIAgent(
            instructions: TriageInstructions,
            name: "triage-agent",
            tools: [
                CustomerLookupTool.Create(customerRepo, orderRepo),
                OrderStatusTool.Create(orderRepo)
            ]);

        var billingSpecialist = chatClient.AsAIAgent(
            instructions: BillingSpecialistInstructions,
            name: "billing-specialist",
            tools: [
                OrderStatusTool.Create(orderRepo),
                EscalateTool.Create()
            ]);

        var shippingSpecialist = chatClient.AsAIAgent(
            instructions: ShippingSpecialistInstructions,
            name: "shipping-specialist",
            tools: [
                OrderStatusTool.Create(orderRepo)
            ]);

        // Handoff workflow: triage routes to billing or shipping.
        // EnableReturnToPrevious() routes subsequent turns back to the last specialist,
        // skipping triage re-classification for follow-up messages in the same session.
        // Disabled when running as a GroupChat participant: the switch graph internals
        // trigger an exception whose TargetSite (MethodBase) cannot be serialized by
        // System.Text.Json when the exception propagates through the GroupChat pipeline.
        var workflowBuilder = AgentWorkflowBuilder
            .CreateHandoffBuilderWith(triageAgent)
            .WithHandoffs(triageAgent, [billingSpecialist, shippingSpecialist]);

        if (!runningAsGroupChatParticipant)
            workflowBuilder.EnableReturnToPrevious();

        var workflow = workflowBuilder.Build();

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
