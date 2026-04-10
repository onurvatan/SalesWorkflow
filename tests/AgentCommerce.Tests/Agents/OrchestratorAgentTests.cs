#pragma warning disable MAAIW001
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Moq;
using SalesWorkflow.Agents;
using Xunit;

namespace SalesWorkflow.Tests.Agents;

public class OrchestratorAgentTests
{
    // ─── TestableOrchestratorManager ─────────────────────────────────────────
    // Exposes protected GroupChatManager callbacks as public so unit tests can
    // exercise them directly without going through the full workflow engine.
    private sealed class TestableOrchestratorManager : OrchestratorGroupChatManager
    {
        public TestableOrchestratorManager(
            IChatClient chatClient,
            IReadOnlyList<AIAgent> participants,
            int maxIterations = 3)
            : base(chatClient, participants)
        {
            MaximumIterationCount = maxIterations;
        }

        public Task<AIAgent> SelectNextAgentPublicAsync(
            IReadOnlyList<ChatMessage> history, CancellationToken ct)
            => SelectNextAgentAsync(history, ct).AsTask();

        public Task<bool> ShouldTerminatePublicAsync(
            IReadOnlyList<ChatMessage> history, CancellationToken ct)
            => ShouldTerminateAsync(history, ct).AsTask();

        public Task<IEnumerable<ChatMessage>> UpdateHistoryPublicAsync(
            IReadOnlyList<ChatMessage> history, CancellationToken ct)
            => UpdateHistoryAsync(history, ct).AsTask();
    }

    // ─── Constants ────────────────────────────────────────────────────────────

    [Fact]
    public void AgentName_IsExpectedValue()
    {
        Assert.Equal("ClientOrchestratorAgent", ClientOrchestratorAgent.AgentName);
    }

    [Fact]
    public void WorkflowDescription_IsNotNullOrEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(ClientOrchestratorAgent.WorkflowDescription));
    }

    [Fact]
    public void WorkflowDescription_MentionsGroupChat()
    {
        Assert.Contains("GroupChat", ClientOrchestratorAgent.WorkflowDescription,
            StringComparison.OrdinalIgnoreCase);
    }

    // ─── CreateAgent ──────────────────────────────────────────────────────────

    [Fact]
    public void CreateAgent_WithTwoParticipants_ReturnsAIAgent()
    {
        var chatClient = new Mock<IChatClient>().Object;
        var customerServiceAgent = BuildStubAgent("CustomerServiceWorkflowAgent");
        var afterSaleAgent = BuildStubAgent("AfterSaleReportWorkflowAgent");

        var orchestratorAgent = new ClientOrchestratorAgent(chatClient);
        var agent = orchestratorAgent.CreateAgent(
            ClientOrchestratorAgent.AgentName,
            [customerServiceAgent, afterSaleAgent]);

        Assert.NotNull(agent);
        Assert.Equal(ClientOrchestratorAgent.AgentName, agent.Name);
    }

    [Fact]
    public void CreateAgent_WithThreeParticipants_ReturnsAIAgent()
    {
        var chatClient = new Mock<IChatClient>().Object;
        var customerServiceAgent = BuildStubAgent("CustomerServiceWorkflowAgent");
        var afterSaleAgent = BuildStubAgent("AfterSaleReportWorkflowAgent");
        var salesAgent = BuildStubAgent("SalesWorkflowAgent");

        var orchestratorAgent = new ClientOrchestratorAgent(chatClient);
        var agent = orchestratorAgent.CreateAgent(
            ClientOrchestratorAgent.AgentName,
            [customerServiceAgent, afterSaleAgent, salesAgent]);

        Assert.NotNull(agent);
        Assert.Equal(ClientOrchestratorAgent.AgentName, agent.Name);
    }

    // ─── OrchestratorGroupChatManager ─────────────────────────────────────────

    [Fact]
    public async Task SelectNextAgentAsync_ReturnsCustomerServiceAgent_ForBillingQuery()
    {
        var customerServiceAgent = BuildStubAgent("CustomerServiceWorkflowAgent");
        var afterSaleAgent = BuildStubAgent("AfterSaleReportWorkflowAgent");
        var participants = new List<AIAgent> { customerServiceAgent, afterSaleAgent };

        var chatClientMock = new Mock<IChatClient>();
        chatClientMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, "CustomerServiceWorkflowAgent")]));

        var manager = new TestableOrchestratorManager(chatClientMock.Object, participants);
        var history = new List<ChatMessage>
        {
            new(ChatRole.User, "I want a refund for my order ORD-004")
        };

        var selected = await manager.SelectNextAgentPublicAsync(history, CancellationToken.None);

        Assert.Equal("CustomerServiceWorkflowAgent", selected.Name);
    }

    [Fact]
    public async Task SelectNextAgentAsync_ReturnsAfterSaleAgent_ForReportQuery()
    {
        var customerServiceAgent = BuildStubAgent("CustomerServiceWorkflowAgent");
        var afterSaleAgent = BuildStubAgent("AfterSaleReportWorkflowAgent");
        var participants = new List<AIAgent> { customerServiceAgent, afterSaleAgent };

        var chatClientMock = new Mock<IChatClient>();
        chatClientMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, "AfterSaleReportWorkflowAgent")]));

        var manager = new TestableOrchestratorManager(chatClientMock.Object, participants);
        var history = new List<ChatMessage>
        {
            new(ChatRole.User, "Show me the monthly sales and customer satisfaction report")
        };

        var selected = await manager.SelectNextAgentPublicAsync(history, CancellationToken.None);

        Assert.Equal("AfterSaleReportWorkflowAgent", selected.Name);
    }

    [Fact]
    public async Task SelectNextAgentAsync_FallsBackToFirstParticipant_WhenLLMReturnsUnknownName()
    {
        var customerServiceAgent = BuildStubAgent("CustomerServiceWorkflowAgent");
        var afterSaleAgent = BuildStubAgent("AfterSaleReportWorkflowAgent");
        var participants = new List<AIAgent> { customerServiceAgent, afterSaleAgent };

        var chatClientMock = new Mock<IChatClient>();
        chatClientMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, "UnknownAgent")]));

        var manager = new TestableOrchestratorManager(chatClientMock.Object, participants);
        var history = new List<ChatMessage>
        {
            new(ChatRole.User, "some ambiguous request")
        };

        var selected = await manager.SelectNextAgentPublicAsync(history, CancellationToken.None);

        // Falls back to first participant
        Assert.Equal("CustomerServiceWorkflowAgent", selected.Name);
    }

    [Fact]
    public void MaximumIterationCount_DefaultsToThree()
    {
        var chatClientMock = new Mock<IChatClient>().Object;
        var participants = new List<AIAgent> { BuildStubAgent("CustomerServiceWorkflowAgent") };
        var manager = new TestableOrchestratorManager(chatClientMock, participants);

        Assert.Equal(3, manager.MaximumIterationCount);
    }

    [Fact]
    public async Task ShouldTerminate_ReturnsTrue_WhenLastMessageContainsDoneSentinel()
    {
        var chatClientMock = new Mock<IChatClient>().Object;
        var participants = new List<AIAgent> { BuildStubAgent("CustomerServiceWorkflowAgent") };
        var manager = new TestableOrchestratorManager(chatClientMock, participants);

        var history = new List<ChatMessage>
        {
            new(ChatRole.Assistant, "Here is your report. [DONE]")
        };

        var result = await manager.ShouldTerminatePublicAsync(history, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task ShouldTerminate_ReturnsFalse_WhenIterationsBelowMax()
    {
        var chatClientMock = new Mock<IChatClient>().Object;
        var participants = new List<AIAgent> { BuildStubAgent("CustomerServiceWorkflowAgent") };
        var manager = new TestableOrchestratorManager(chatClientMock, participants);

        var result = await manager.ShouldTerminatePublicAsync(
            [new ChatMessage(ChatRole.User, "hello")],
            CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task UpdateHistoryAsync_ReturnsHistoryUnchanged()
    {
        var chatClientMock = new Mock<IChatClient>().Object;
        var participants = new List<AIAgent> { BuildStubAgent("CustomerServiceWorkflowAgent") };
        var manager = new TestableOrchestratorManager(chatClientMock, participants);

        var history = new List<ChatMessage> { new(ChatRole.User, "test") };
        var result = await manager.UpdateHistoryPublicAsync(history, CancellationToken.None);

        Assert.Equal(history, result);
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

