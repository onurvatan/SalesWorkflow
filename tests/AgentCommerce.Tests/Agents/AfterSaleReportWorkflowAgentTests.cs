#pragma warning disable MAAIW001
using SalesWorkflow.Agents;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Moq;
using Xunit;

namespace SalesWorkflow.Tests.Agents;

public class AfterSaleReportWorkflowAgentTests
{
    [Fact]
    public void AgentName_IsExpectedValue()
    {
        Assert.Equal("AfterSaleReportWorkflowAgent", AfterSaleReportWorkflowAgent.AgentName);
    }

    [Fact]
    public void WorkflowDescription_IsNotNullOrEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(AfterSaleReportWorkflowAgent.WorkflowDescription));
    }

    [Fact]
    public void SalesAnalystInstructions_ReferencesSalesReportTool()
    {
        Assert.Contains("sales_report", AfterSaleReportWorkflowAgent.SalesAnalystInstructions,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SatisfactionAnalystInstructions_ReferencesCustomerSatisfactionTool()
    {
        Assert.Contains("customer_satisfaction_report",
            AfterSaleReportWorkflowAgent.SatisfactionAnalystInstructions,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WorkflowDescription_MentionsConcurrent()
    {
        Assert.Contains("Concurrent", AfterSaleReportWorkflowAgent.WorkflowDescription,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_ReturnsAgent_WithCorrectName()
    {
        var a1 = new Mock<AIAgent>().Object;
        var a2 = new Mock<AIAgent>().Object;
        var workflow = AgentWorkflowBuilder.BuildConcurrent(
            AfterSaleReportWorkflowAgent.AgentName,
            [a1, a2]);

        var agent = AfterSaleReportWorkflowAgent.Create(workflow, AfterSaleReportWorkflowAgent.AgentName);

        Assert.Equal(AfterSaleReportWorkflowAgent.AgentName, agent.Name);
    }

    [Fact]
    public void Create_ReturnsAgent_WithWorkflowDescription()
    {
        var a1 = new Mock<AIAgent>().Object;
        var a2 = new Mock<AIAgent>().Object;
        var workflow = AgentWorkflowBuilder.BuildConcurrent(
            AfterSaleReportWorkflowAgent.AgentName,
            [a1, a2]);

        var agent = AfterSaleReportWorkflowAgent.Create(workflow, AfterSaleReportWorkflowAgent.AgentName);

        Assert.Equal(AfterSaleReportWorkflowAgent.WorkflowDescription, agent.Description);
    }
}
