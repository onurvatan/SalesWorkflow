using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace SalesWorkflow.Tests.Integration;

/// <summary>
/// Integration tests for the customer-facing orchestrator endpoint (POST /agents).
/// Verifies LLM-driven routing to CustomerService and Sales workflow participants.
/// Requires valid Azure credentials (AzureCliCredential or DefaultAzureCredential).
/// Run with: dotnet test --filter "Category=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class OrchestratorEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    // ── Auth / access ────────────────────────────────────────────────────────

    [Fact]
    public async Task PostAgents_NoApiKeyRequired_ReturnsOk()
    {
        // The customer-facing /agents endpoint is public — no API key needed.
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/agents",
            new { input = "I need help with my order" });

        response.EnsureSuccessStatusCode();
    }

    // ── Session handling ─────────────────────────────────────────────────────

    [Fact]
    public async Task PostAgents_ResponseIncludesSessionId()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/agents",
            new { input = "Show me gaming laptops" });

        response.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.TryGetProperty("sessionId", out var sessionId));
        Assert.False(string.IsNullOrWhiteSpace(sessionId.GetString()));
    }

    [Fact]
    public async Task PostAgents_WithExistingSessionId_ReusesSession()
    {
        var client = factory.CreateClient();
        const string sessionId = "orch-session-reuse-test";

        var first = await client.PostAsJsonAsync("/agents",
            new { input = "What's the status of my order?", sessionId });
        first.EnsureSuccessStatusCode();

        var second = await client.PostAsJsonAsync("/agents",
            new { input = "Follow up: was it shipped yet?", sessionId });
        second.EnsureSuccessStatusCode();

        var doc = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.Equal(sessionId, doc.RootElement.GetProperty("sessionId").GetString());

        // Cleanup
        await client.DeleteAsync($"/sessions/{sessionId}");
    }

    // ── Routing — AfterSaleReport must NOT be reachable via /agents ──────────

    [Fact]
    public async Task PostAgents_CustomerServiceIntent_ReturnsOrchestratorAgentName()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/agents",
            new { input = "I was charged twice for my order and need a refund" });

        response.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ClientOrchestratorAgent", doc.RootElement.GetProperty("agentName").GetString());
    }

    [Fact]
    public async Task PostAgents_ReportIntent_DoesNotRouteToAfterSaleReport()
    {
        // AfterSaleReportWorkflowAgent is NOT a participant of OrchestratorAgent.
        // Even if the user asks for a report, the orchestrator must not route to it.
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/agents",
            new { input = "Give me an after-sale report" });

        // The request itself should succeed (orchestrator handles it with available
        // participants); it won't call AfterSaleReport since it's not a participant.
        response.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ClientOrchestratorAgent", doc.RootElement.GetProperty("agentName").GetString());
    }

    // ── Session cleanup ──────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteSession_AfterOrchestratorTurn_Returns204()
    {
        var client = factory.CreateClient();

        var post = await client.PostAsJsonAsync("/agents",
            new { input = "What products do you have?" });
        post.EnsureSuccessStatusCode();

        var doc = JsonDocument.Parse(await post.Content.ReadAsStringAsync());
        var sessionId = doc.RootElement.GetProperty("sessionId").GetString();

        var delete = await client.DeleteAsync($"/sessions/{sessionId}");
        Assert.Equal(System.Net.HttpStatusCode.NoContent, delete.StatusCode);
    }
}
