using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using Xunit;

namespace SalesWorkflow.Tests.Integration;

/// <summary>
/// Integration tests for the customer-service endpoint.
/// Requires a running Azure environment with valid credentials (AzureCliCredential or DefaultAzureCredential).
/// Run with: dotnet test --filter "Category=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class CustomerServiceEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task PostCustomerService_WithShippingIssue_ReturnsOk()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/agents/customer-service",
            new { input = "My order ORD-002 hasn't arrived yet, I need help with delivery" });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(body));
    }

    [Fact]
    public async Task PostCustomerService_WithBillingIssue_ReturnsOk()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/agents/customer-service",
            new { input = "I want to request a refund for order ORD-004 which was cancelled" });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(body));
    }
}
