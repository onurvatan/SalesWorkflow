using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace SalesWorkflow.Tests.Integration;

/// <summary>
/// Integration tests for the sales-workflow endpoint.
/// Requires a running Azure environment with valid credentials (AzureCliCredential or DefaultAzureCredential).
/// Run with: dotnet test --filter "Category=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class WorkflowEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task PostSalesWorkflow_WithValidInput_ReturnsOk()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/agents/sales-workflow",
            new { input = "Show me laptops under $1500" });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(body));
    }

    [Fact]
    public async Task PostSalesWorkflow_ResponseIncludesSessionId()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/agents/sales-workflow",
            new { input = "Show me gaming monitors" });

        response.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.TryGetProperty("sessionId", out var sessionId));
        Assert.False(string.IsNullOrWhiteSpace(sessionId.GetString()));
    }

    [Fact]
    public async Task DeleteSession_AfterFirstTurn_Returns204()
    {
        var client = factory.CreateClient();

        // First turn — creates the session
        var post = await client.PostAsJsonAsync("/agents/sales-workflow",
            new { input = "Show me keyboards" });
        post.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(await post.Content.ReadAsStringAsync());
        var sessionId = doc.RootElement.GetProperty("sessionId").GetString();

        // Delete the session
        var delete = await client.DeleteAsync($"/sessions/{sessionId}");
        Assert.Equal(System.Net.HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task DeleteSession_UnknownSession_Returns404()
    {
        var client = factory.CreateClient();

        var response = await client.DeleteAsync("/sessions/nonexistent-session-id");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }
}
