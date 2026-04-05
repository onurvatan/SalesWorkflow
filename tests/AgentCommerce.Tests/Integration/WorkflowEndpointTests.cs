using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
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
}
