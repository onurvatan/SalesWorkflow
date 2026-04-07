#pragma warning disable MAAIW001
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Moq;
using SalesWorkflow.Agents;
using Xunit;

namespace SalesWorkflow.Tests.Agents;

public class BackOfficeOrchestratorAgentTests
{
    // ─── Constants ────────────────────────────────────────────────────────────

    [Fact]
    public void AgentName_IsExpectedValue()
    {
        Assert.Equal("BackOfficeOrchestratorAgent", BackOfficeOrchestratorAgent.AgentName);
    }

    [Fact]
    public void WorkflowDescription_IsNotNullOrEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(BackOfficeOrchestratorAgent.WorkflowDescription));
    }

    [Fact]
    public void WorkflowDescription_MentionsGroupChat()
    {
        Assert.Contains("GroupChat", BackOfficeOrchestratorAgent.WorkflowDescription,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WorkflowDescription_MentionsAfterSaleReport()
    {
        Assert.Contains("AfterSaleReport", BackOfficeOrchestratorAgent.WorkflowDescription,
            StringComparison.OrdinalIgnoreCase);
    }

    // ─── CreateAgent ──────────────────────────────────────────────────────────

    [Fact]
    public void CreateAgent_WithAfterSaleParticipant_ReturnsAIAgent()
    {
        var chatClient = new Mock<IChatClient>().Object;
        var afterSaleAgent = BuildStubAgent("AfterSaleReportWorkflowAgent");

        var backOfficeAgent = new BackOfficeOrchestratorAgent(chatClient);
        var agent = backOfficeAgent.CreateAgent(
            BackOfficeOrchestratorAgent.AgentName,
            [afterSaleAgent]);

        Assert.NotNull(agent);
        Assert.Equal(BackOfficeOrchestratorAgent.AgentName, agent.Name);
    }

    [Fact]
    public void CreateAgent_WithMultipleParticipants_ReturnsAIAgent()
    {
        var chatClient = new Mock<IChatClient>().Object;
        var afterSaleAgent = BuildStubAgent("AfterSaleReportWorkflowAgent");
        var secondAgent = BuildStubAgent("AnotherAdminAgent");

        var backOfficeAgent = new BackOfficeOrchestratorAgent(chatClient);
        var agent = backOfficeAgent.CreateAgent(
            BackOfficeOrchestratorAgent.AgentName,
            [afterSaleAgent, secondAgent]);

        Assert.NotNull(agent);
        Assert.Equal(BackOfficeOrchestratorAgent.AgentName, agent.Name);
    }

    [Fact]
    public void AgentName_DiffersFromCustomerFacingOrchestratorName()
    {
        Assert.NotEqual(OrchestratorAgent.AgentName, BackOfficeOrchestratorAgent.AgentName);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a stub <see cref="AIAgent"/> backed by a Round-Robin GroupChat workflow
    /// with a single no-op participant, sufficient for name-matching tests.
    /// </summary>
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

        return workflow.AsAIAgent(
            name,
            name,
            $"Stub agent for {name}",
            InProcessExecution.OffThread);
    }
}
