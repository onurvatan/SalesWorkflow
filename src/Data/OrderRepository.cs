using SalesWorkflow.Models;

namespace SalesWorkflow.Data;

public record ProductSalesSummary(string ProductSku, string ProductName, int UnitsSold, decimal Revenue);

public record SalesSummary(
    int TotalOrders,
    decimal TotalRevenue,
    string Currency,
    IReadOnlyDictionary<string, int> OrdersByStatus,
    IReadOnlyList<ProductSalesSummary> TopProducts);

public interface IOrderRepository
{
    IReadOnlyList<Order> GetAll();
    IReadOnlyList<Order> FindByCustomerId(string customerId);
    Order? FindByOrderId(string orderId);
    SalesSummary GetSalesSummary();
}

public class OrderRepository : IOrderRepository
{
    private readonly IReadOnlyList<Order> _orders =
    [
        new()
        {
            Id          = "order-001",
            OrderId     = "ORD-001",
            CustomerId  = "CUST-001",
            ProductSku  = "DELL-XPS15-2025",
            ProductName = "Dell XPS 15 (2025)",
            Quantity    = 1,
            TotalAmount = 1_799.99m,
            Currency    = "USD",
            OrderDate   = new DateTime(2026, 1, 10),
            Status      = OrderStatus.Delivered
        },
        new()
        {
            Id          = "order-002",
            OrderId     = "ORD-002",
            CustomerId  = "CUST-001",
            ProductSku  = "APPLE-IP17P-256",
            ProductName = "Apple iPhone 17 Pro 256 GB",
            Quantity    = 1,
            TotalAmount = 1_199.00m,
            Currency    = "USD",
            OrderDate   = new DateTime(2026, 2, 5),
            Status      = OrderStatus.Shipped
        },
        new()
        {
            Id          = "order-003",
            OrderId     = "ORD-003",
            CustomerId  = "CUST-002",
            ProductSku  = "SAMSUNG-S25U-256",
            ProductName = "Samsung Galaxy S25 Ultra 256 GB",
            Quantity    = 1,
            TotalAmount = 1_299.99m,
            Currency    = "USD",
            OrderDate   = new DateTime(2026, 1, 22),
            Status      = OrderStatus.Delivered
        },
        new()
        {
            Id          = "order-004",
            OrderId     = "ORD-004",
            CustomerId  = "CUST-002",
            ProductSku  = "HP-SPEC-X360-14",
            ProductName = "HP Spectre x360 14",
            Quantity    = 1,
            TotalAmount = 1_549.99m,
            Currency    = "USD",
            OrderDate   = new DateTime(2026, 2, 14),
            Status      = OrderStatus.Cancelled
        },
        new()
        {
            Id          = "order-005",
            OrderId     = "ORD-005",
            CustomerId  = "CUST-003",
            ProductSku  = "APPLE-MBP14-M4",
            ProductName = "Apple MacBook Pro 14\" M4 Pro",
            Quantity    = 1,
            TotalAmount = 1_999.00m,
            Currency    = "USD",
            OrderDate   = new DateTime(2025, 12, 18),
            Status      = OrderStatus.Delivered
        },
        new()
        {
            Id          = "order-006",
            OrderId     = "ORD-006",
            CustomerId  = "CUST-003",
            ProductSku  = "SAMSUNG-S25U-256",
            ProductName = "Samsung Galaxy S25 Ultra 256 GB",
            Quantity    = 2,
            TotalAmount = 2_599.98m,
            Currency    = "USD",
            OrderDate   = new DateTime(2026, 1, 30),
            Status      = OrderStatus.Shipped
        },
        new()
        {
            Id          = "order-007",
            OrderId     = "ORD-007",
            CustomerId  = "CUST-004",
            ProductSku  = "ASUS-ROG-G14-2025",
            ProductName = "ASUS ROG Zephyrus G14 (2025)",
            Quantity    = 1,
            TotalAmount = 1_649.99m,
            Currency    = "USD",
            OrderDate   = new DateTime(2026, 3, 1),
            Status      = OrderStatus.Pending
        },
        new()
        {
            Id          = "order-008",
            OrderId     = "ORD-008",
            CustomerId  = "CUST-004",
            ProductSku  = "MS-SURFL7-15",
            ProductName = "Microsoft Surface Laptop 7 15\"",
            Quantity    = 1,
            TotalAmount = 1_399.99m,
            Currency    = "USD",
            OrderDate   = new DateTime(2025, 11, 5),
            Status      = OrderStatus.Delivered
        },
        new()
        {
            Id          = "order-009",
            OrderId     = "ORD-009",
            CustomerId  = "CUST-005",
            ProductSku  = "APPLE-MBP14-M4",
            ProductName = "Apple MacBook Pro 14\" M4 Pro",
            Quantity    = 1,
            TotalAmount = 1_999.00m,
            Currency    = "USD",
            OrderDate   = new DateTime(2026, 2, 20),
            Status      = OrderStatus.Delivered
        },
        new()
        {
            Id          = "order-010",
            OrderId     = "ORD-010",
            CustomerId  = "CUST-005",
            ProductSku  = "APPLE-IP17P-256",
            ProductName = "Apple iPhone 17 Pro 256 GB",
            Quantity    = 1,
            TotalAmount = 1_199.00m,
            Currency    = "USD",
            OrderDate   = new DateTime(2026, 3, 8),
            Status      = OrderStatus.Delivered
        },
    ];

    public IReadOnlyList<Order> GetAll() => _orders;

    public IReadOnlyList<Order> FindByCustomerId(string customerId) =>
        _orders
            .Where(o => o.CustomerId.Equals(customerId, StringComparison.OrdinalIgnoreCase))
            .ToList();

    public Order? FindByOrderId(string orderId) =>
        _orders.FirstOrDefault(o => o.OrderId.Equals(orderId, StringComparison.OrdinalIgnoreCase));

    public SalesSummary GetSalesSummary()
    {
        var delivered = _orders.Where(o => o.Status != OrderStatus.Cancelled).ToList();

        var byStatus = _orders
            .GroupBy(o => o.Status.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var topProducts = delivered
            .GroupBy(o => new { o.ProductSku, o.ProductName })
            .Select(g => new ProductSalesSummary(
                g.Key.ProductSku,
                g.Key.ProductName,
                g.Sum(o => o.Quantity),
                g.Sum(o => o.TotalAmount)))
            .OrderByDescending(p => p.Revenue)
            .Take(5)
            .ToList();

        return new SalesSummary(
            TotalOrders: _orders.Count,
            TotalRevenue: delivered.Sum(o => o.TotalAmount),
            Currency: "USD",
            OrdersByStatus: byStatus,
            TopProducts: topProducts);
    }
}
