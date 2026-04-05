using SalesWorkflow.Data;
using SalesWorkflow.Tools;
using Xunit;

namespace SalesWorkflow.Tests.Tools;

public class CustomerLookupToolTests
{
    private readonly ICustomerRepository _customers = new CustomerRepository();
    private readonly IOrderRepository _orders = new OrderRepository();

    [Fact]
    public void Create_ReturnsFunction_WithCorrectName()
    {
        var tool = CustomerLookupTool.Create(_customers, _orders);
        Assert.Equal("customer_lookup", tool.Metadata.Name);
    }

    [Fact]
    public void Create_ReturnsFunction_WithNonEmptyDescription()
    {
        var tool = CustomerLookupTool.Create(_customers, _orders);
        Assert.False(string.IsNullOrWhiteSpace(tool.Metadata.Description));
    }

    [Fact]
    public async Task InvokeAsync_ByCustomerId_ReturnsCustomerData()
    {
        var tool = CustomerLookupTool.Create(_customers, _orders);

        var result = await tool.InvokeAsync(new() { ["query"] = "CUST-001" });

        var text = result?.ToString() ?? string.Empty;
        Assert.Contains("CUST-001", text);
        Assert.Contains("Alice", text);
    }

    [Fact]
    public async Task InvokeAsync_ByName_ReturnsCustomerData()
    {
        var tool = CustomerLookupTool.Create(_customers, _orders);

        var result = await tool.InvokeAsync(new() { ["query"] = "Bob" });

        var text = result?.ToString() ?? string.Empty;
        Assert.Contains("Bob", text);
    }

    [Fact]
    public async Task InvokeAsync_NoMatch_ReturnsNotFoundMessage()
    {
        var tool = CustomerLookupTool.Create(_customers, _orders);

        var result = await tool.InvokeAsync(new() { ["query"] = "NONEXISTENT-999" });

        var text = result?.ToString() ?? string.Empty;
        Assert.Contains("No customer found", text);
    }
}
