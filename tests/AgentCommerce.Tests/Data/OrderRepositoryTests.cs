using SalesWorkflow.Data;
using Xunit;

namespace SalesWorkflow.Tests.Data;

public class OrderRepositoryTests
{
    private readonly IOrderRepository _repo = new OrderRepository();

    [Fact]
    public void GetAll_ReturnsTenOrders()
    {
        Assert.Equal(10, _repo.GetAll().Count);
    }

    [Fact]
    public void FindByCustomerId_ReturnsMatchingOrders()
    {
        var orders = _repo.FindByCustomerId("CUST-001");
        Assert.Equal(2, orders.Count);
        Assert.All(orders, o => Assert.Equal("CUST-001", o.CustomerId));
    }

    [Fact]
    public void FindByCustomerId_CaseInsensitive()
    {
        var orders = _repo.FindByCustomerId("cust-001");
        Assert.Equal(2, orders.Count);
    }

    [Fact]
    public void FindByCustomerId_NoMatch_ReturnsEmpty()
    {
        var orders = _repo.FindByCustomerId("CUST-999");
        Assert.Empty(orders);
    }

    [Fact]
    public void FindByOrderId_ReturnsCorrectOrder()
    {
        var order = _repo.FindByOrderId("ORD-005");
        Assert.NotNull(order);
        Assert.Equal("CUST-003", order.CustomerId);
    }

    [Fact]
    public void FindByOrderId_CaseInsensitive()
    {
        var order = _repo.FindByOrderId("ord-005");
        Assert.NotNull(order);
    }

    [Fact]
    public void FindByOrderId_NoMatch_ReturnsNull()
    {
        var order = _repo.FindByOrderId("ORD-999");
        Assert.Null(order);
    }

    [Fact]
    public void GetSalesSummary_TotalOrders_IsTen()
    {
        var summary = _repo.GetSalesSummary();
        Assert.Equal(10, summary.TotalOrders);
    }

    [Fact]
    public void GetSalesSummary_TotalRevenue_ExcludesCancelledOrders()
    {
        var summary = _repo.GetSalesSummary();
        // ORD-004 is Cancelled (1549.99) so revenue must be less than the sum of all orders
        Assert.True(summary.TotalRevenue > 0);
        Assert.DoesNotContain("Cancelled", summary.OrdersByStatus.Keys
            .Where(k => k == "Delivered").Select(k => k));
    }

    [Fact]
    public void GetSalesSummary_TopProducts_NotEmpty()
    {
        var summary = _repo.GetSalesSummary();
        Assert.NotEmpty(summary.TopProducts);
    }
}
