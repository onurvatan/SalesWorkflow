using SalesWorkflow.Data;
using SalesWorkflow.Tools;
using Xunit;

namespace SalesWorkflow.Tests.Tools;

public class SalesReportToolTests
{
    private readonly IOrderRepository _orders = new OrderRepository();

    [Fact]
    public void Create_ReturnsFunction_WithCorrectName()
    {
        var tool = SalesReportTool.Create(_orders);
        Assert.Equal("sales_report", tool.Name);
    }

    [Fact]
    public async Task InvokeAsync_ReturnsNonEmptyJson()
    {
        var tool = SalesReportTool.Create(_orders);

        var result = await tool.InvokeAsync(new() { ["dateRange"] = "" });

        var text = result?.ToString() ?? string.Empty;
        Assert.False(string.IsNullOrWhiteSpace(text));
    }

    [Fact]
    public async Task InvokeAsync_ContainsTotalOrders()
    {
        var tool = SalesReportTool.Create(_orders);

        var result = await tool.InvokeAsync(new() { ["dateRange"] = "" });

        var text = result?.ToString() ?? string.Empty;
        Assert.Contains("totalOrders", text);
    }

    [Fact]
    public async Task InvokeAsync_ContainsTopProducts()
    {
        var tool = SalesReportTool.Create(_orders);

        var result = await tool.InvokeAsync(new() { ["dateRange"] = "" });

        var text = result?.ToString() ?? string.Empty;
        Assert.Contains("topProducts", text);
    }

    [Fact]
    public async Task InvokeAsync_TotalOrders_MatchesSeedData()
    {
        var tool = SalesReportTool.Create(_orders);

        var result = await tool.InvokeAsync(new() { ["dateRange"] = "" });

        // Seeded with 10 orders
        var text = result?.ToString() ?? string.Empty;
        Assert.Contains("\"totalOrders\":10", text);
    }
}
