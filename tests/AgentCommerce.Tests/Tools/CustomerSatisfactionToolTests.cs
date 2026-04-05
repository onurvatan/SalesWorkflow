using SalesWorkflow.Data;
using SalesWorkflow.Tools;
using Xunit;

namespace SalesWorkflow.Tests.Tools;

public class CustomerSatisfactionToolTests
{
    private readonly ICustomerRepository _customers = new CustomerRepository();

    [Fact]
    public void Create_ReturnsFunction_WithCorrectName()
    {
        var tool = CustomerSatisfactionTool.Create(_customers);
        Assert.Equal("customer_satisfaction_report", tool.Name);
    }

    [Fact]
    public async Task InvokeAsync_ReturnsNonEmptyJson()
    {
        var tool = CustomerSatisfactionTool.Create(_customers);

        var result = await tool.InvokeAsync(new() { ["tierFilter"] = "" });

        var text = result?.ToString() ?? string.Empty;
        Assert.False(string.IsNullOrWhiteSpace(text));
    }

    [Fact]
    public async Task InvokeAsync_ContainsAverageScore()
    {
        var tool = CustomerSatisfactionTool.Create(_customers);

        var result = await tool.InvokeAsync(new() { ["tierFilter"] = "" });

        var text = result?.ToString() ?? string.Empty;
        Assert.Contains("averageScore", text);
    }

    [Fact]
    public async Task InvokeAsync_ContainsAtRiskCustomers()
    {
        var tool = CustomerSatisfactionTool.Create(_customers);

        var result = await tool.InvokeAsync(new() { ["tierFilter"] = "" });

        var text = result?.ToString() ?? string.Empty;
        Assert.Contains("atRiskCustomers", text);
        // David Lee has score 2 — should appear in at-risk list
        Assert.Contains("David", text);
    }

    [Fact]
    public async Task InvokeAsync_TotalCustomers_MatchesSeedData()
    {
        var tool = CustomerSatisfactionTool.Create(_customers);

        var result = await tool.InvokeAsync(new() { ["tierFilter"] = "" });

        var text = result?.ToString() ?? string.Empty;
        // Seeded with 5 customers
        Assert.Contains("\"totalCustomers\":5", text);
    }
}
