using SalesWorkflow.Data;
using SalesWorkflow.Tools;
using Xunit;

namespace SalesWorkflow.Tests.Tools;

public class OrderStatusToolTests
{
    private readonly IOrderRepository _orders = new OrderRepository();

    [Fact]
    public void Create_ReturnsFunction_WithCorrectName()
    {
        var tool = OrderStatusTool.Create(_orders);
        Assert.Equal("order_status", tool.Metadata.Name);
    }

    [Fact]
    public async Task InvokeAsync_ByOrderId_ReturnsOrderData()
    {
        var tool = OrderStatusTool.Create(_orders);

        var result = await tool.InvokeAsync(new() { ["query"] = "ORD-001" });

        var text = result?.ToString() ?? string.Empty;
        Assert.Contains("ORD-001", text);
        Assert.Contains("Delivered", text);
    }

    [Fact]
    public async Task InvokeAsync_ByCustomerId_ReturnsMultipleOrders()
    {
        var tool = OrderStatusTool.Create(_orders);

        var result = await tool.InvokeAsync(new() { ["query"] = "CUST-001" });

        var text = result?.ToString() ?? string.Empty;
        Assert.Contains("CUST-001", text);
        // CUST-001 has 2 orders
        Assert.Contains("ORD-001", text);
        Assert.Contains("ORD-002", text);
    }

    [Fact]
    public async Task InvokeAsync_NoMatch_ReturnsNotFoundMessage()
    {
        var tool = OrderStatusTool.Create(_orders);

        var result = await tool.InvokeAsync(new() { ["query"] = "ORD-999" });

        var text = result?.ToString() ?? string.Empty;
        Assert.Contains("No orders found", text);
    }
}
