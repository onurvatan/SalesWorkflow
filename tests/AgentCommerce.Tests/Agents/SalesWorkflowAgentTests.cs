using SalesWorkflow.Agents;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Moq;
using Xunit;

namespace SalesWorkflow.Tests.Agents;

public class SalesWorkflowAgentTests
{
    [Fact]
    public void AgentName_IsExpectedValue()
    {
        Assert.Equal("SalesWorkflowAgent", SalesWorkflowAgent.AgentName);
    }

    [Fact]
    public void CatalogRetrieverInstructions_IsNotNullOrEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(SalesWorkflowAgent.CatalogRetrieverInstructions));
    }

    [Fact]
    public void CatalogRetrieverInstructions_ReferencesCatalogSearchTool()
    {
        Assert.Contains("catalog_search", SalesWorkflowAgent.CatalogRetrieverInstructions, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StockCheckerInstructions_IsNotNullOrEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(SalesWorkflowAgent.StockCheckerInstructions));
    }

    [Fact]
    public void StockCheckerInstructions_ReferencesStockCheckTool()
    {
        Assert.Contains("stock_check", SalesWorkflowAgent.StockCheckerInstructions, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SalesResponderInstructions_IsNotNullOrEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(SalesWorkflowAgent.SalesResponderInstructions));
    }

    [Fact]
    public void SalesResponderInstructions_ContainsRecommendationLanguage()
    {
        Assert.Contains("recommendation", SalesWorkflowAgent.SalesResponderInstructions, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WorkflowDescription_IsNotNullOrEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(SalesWorkflowAgent.WorkflowDescription));
    }

    [Fact]
    public void Create_ReturnsAgent_WithCorrectName()
    {
        var workflow = AgentWorkflowBuilder.BuildSequential("test",
            [new Mock<AIAgent>().Object, new Mock<AIAgent>().Object, new Mock<AIAgent>().Object]);

        var agent = SalesWorkflowAgent.Create(workflow, SalesWorkflowAgent.AgentName);

        Assert.Equal(SalesWorkflowAgent.AgentName, agent.Name);
    }

    [Fact]
    public void Create_ReturnsAgent_WithWorkflowDescription()
    {
        var workflow = AgentWorkflowBuilder.BuildSequential("test",
            [new Mock<AIAgent>().Object, new Mock<AIAgent>().Object, new Mock<AIAgent>().Object]);

        var agent = SalesWorkflowAgent.Create(workflow, SalesWorkflowAgent.AgentName);

        Assert.Equal(SalesWorkflowAgent.WorkflowDescription, agent.Description);
    }
}
