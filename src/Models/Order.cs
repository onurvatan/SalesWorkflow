namespace SalesWorkflow.Models;

public enum OrderStatus
{
    Pending,
    Shipped,
    Delivered,
    Cancelled
}

public class Order
{
    public string Id { get; init; } = default!;
    public string OrderId { get; init; } = default!;
    public string CustomerId { get; init; } = default!;
    public string ProductSku { get; init; } = default!;
    public string ProductName { get; init; } = default!;
    public int Quantity { get; init; }
    public decimal TotalAmount { get; init; }
    public string Currency { get; init; } = "USD";
    public DateTime OrderDate { get; init; }
    public OrderStatus Status { get; init; }
}
