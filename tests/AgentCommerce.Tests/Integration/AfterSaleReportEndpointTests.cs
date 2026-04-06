using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace SalesWorkflow.Tests.Integration;

/// <summary>
/// Integration tests for the after-sale-report endpoint.
/// Requires a running Azure environment with valid credentials (AzureCliCredential or DefaultAzureCredential).
/// Run with: dotnet test --filter "Category=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class AfterSaleReportEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task PostAfterSaleReport_RequestsAdminReport_ReturnsOk()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/agents/after-sale-report",
            new { input = "Give me the full monthly sales and customer satisfaction report" });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(body));
    }

    [Fact]
    public async Task PostAfterSaleReport_ReturnsAgentName()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/agents/after-sale-report",
            new { input = "Show me revenue trends and at-risk customers" });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("AfterSaleReportWorkflowAgent", body);
    }

    [Fact]
    public async Task PostAfterSaleReport_ResponseIncludesSessionId()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/agents/after-sale-report",
            new { input = "Weekly report please" });

        response.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.TryGetProperty("sessionId", out var sessionId));
        Assert.False(string.IsNullOrWhiteSpace(sessionId.GetString()));
    }
}
