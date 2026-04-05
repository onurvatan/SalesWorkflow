#pragma warning disable MAAIW001
using SalesWorkflow.Agents;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Moq;
using Xunit;

namespace SalesWorkflow.Tests.Agents;

public class CustomerServiceWorkflowAgentTests
{
    [Fact]
    public void AgentName_IsExpectedValue()
    {
        Assert.Equal("CustomerServiceWorkflowAgent", CustomerServiceWorkflowAgent.AgentName);
    }

    [Fact]
    public void WorkflowDescription_IsNotNullOrEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(CustomerServiceWorkflowAgent.WorkflowDescription));
    }

    [Fact]
    public void TriageInstructions_ReferencesCustomerLookupTool()
    {
        Assert.Contains("customer_lookup", CustomerServiceWorkflowAgent.TriageInstructions,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TriageInstructions_ReferencesOrderStatusTool()
    {
        Assert.Contains("order_status", CustomerServiceWorkflowAgent.TriageInstructions,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BillingSpecialistInstructions_ReferencesEscalateTool()
    {
        Assert.Contains("escalate_to_human", CustomerServiceWorkflowAgent.BillingSpecialistInstructions,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShippingSpecialistInstructions_ReferencesOrderStatusTool()
    {
        Assert.Contains("order_status", CustomerServiceWorkflowAgent.ShippingSpecialistInstructions,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WorkflowDescription_MentionsHandoff()
    {
        Assert.Contains("Handoff", CustomerServiceWorkflowAgent.WorkflowDescription,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_ReturnsAgent_WithCorrectName()
    {
        var triage = BuildStubAgent("TriageAgent");
        var billing = BuildStubAgent("BillingSpecialistAgent");
        var workflow = AgentWorkflowBuilder
            .CreateHandoffBuilderWith(triage)
            .WithHandoffs(triage, [billing])
            .Build();

        var agent = CustomerServiceWorkflowAgent.Create(workflow, CustomerServiceWorkflowAgent.AgentName);

        Assert.Equal(CustomerServiceWorkflowAgent.AgentName, agent.Name);
    }

    [Fact]
    public void Create_ReturnsAgent_WithWorkflowDescription()
    {
        var triage = BuildStubAgent("TriageAgent");
        var billing = BuildStubAgent("BillingSpecialistAgent");
        var workflow = AgentWorkflowBuilder
            .CreateHandoffBuilderWith(triage)
            .WithHandoffs(triage, [billing])
            .Build();

        var agent = CustomerServiceWorkflowAgent.Create(workflow, CustomerServiceWorkflowAgent.AgentName);

        Assert.Equal(CustomerServiceWorkflowAgent.WorkflowDescription, agent.Description);
    }

    private static AIAgent BuildStubAgent(string name)
    {
        var stubParticipant = new Mock<AIAgent>().Object;
        var workflow = AgentWorkflowBuilder
            .CreateGroupChatBuilderWith(participants =>
                new RoundRobinGroupChatManager(participants,
                    (_, _, _) => ValueTask.FromResult(true)))
            .AddParticipants([stubParticipant])
            .WithName(name)
            .Build();

        return workflow.AsAIAgent(name, name, $"Stub agent for {name}", InProcessExecution.OffThread);
    }
}
