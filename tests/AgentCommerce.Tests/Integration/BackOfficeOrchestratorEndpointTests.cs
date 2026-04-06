using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SalesWorkflow.Configuration;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace SalesWorkflow.Tests.Integration;

/// <summary>
/// Integration tests for the back-office orchestrator endpoint (POST /admin/agents).
/// Verifies API key enforcement and routing to AfterSaleReportWorkflowAgent.
/// Auth rejection tests (401) do not call Azure — the filter fires before the handler.
/// Full routing tests require valid Azure credentials.
/// Run with: dotnet test --filter "Category=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class BackOfficeOrchestratorEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string AdminEndpoint = "/admin/agents";

    // The key defined in appsettings.Development.json for the test environment
    private const string ValidApiKey = "dev-backoffice-key-12345";
    private const string WrongApiKey = "totally-wrong-key";

    // ── API key enforcement ──────────────────────────────────────────────────

    [Fact]
    public async Task PostAdminAgents_NoApiKey_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(AdminEndpoint,
            new { input = "Generate after-sale report" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostAdminAgents_WrongApiKey_Returns401()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", WrongApiKey);

        var response = await client.PostAsJsonAsync(AdminEndpoint,
            new { input = "Generate after-sale report" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostAdminAgents_WithValidApiKey_ReturnsOk()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);

        var response = await client.PostAsJsonAsync(AdminEndpoint,
            new { input = "Generate after-sale report" });

        response.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("BackOfficeOrchestratorAgent", doc.RootElement.GetProperty("agentName").GetString());
    }

    [Fact]
    public async Task PostAdminAgents_ValidApiKey_ResponseIncludesSessionId()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);

        var response = await client.PostAsJsonAsync(AdminEndpoint,
            new { input = "Summarise sales performance" });

        response.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.TryGetProperty("sessionId", out var sessionId));
        Assert.False(string.IsNullOrWhiteSpace(sessionId.GetString()));

        // Cleanup
        var sid = sessionId.GetString();
        await client.DeleteAsync($"/sessions/{sid}");
    }

    // ── Public endpoints are unaffected by the admin filter ─────────────────

    [Fact]
    public async Task PostAgents_PublicEndpoint_NotAffectedByApiKeyFilter()
    {
        // POST /agents has no API key requirement even though /admin/agents does.
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/agents",
            new { input = "I need a refund for order #123" });

        // Must not return 401 — public endpoint must remain open
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
