namespace SalesWorkflow.Models;

public enum CustomerTier
{
    Standard,
    Premium,
    VIP
}

public class Customer
{
    public string Id { get; init; } = default!;
    public string CustomerId { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string Email { get; init; } = default!;
    public CustomerTier Tier { get; init; }
    public int TotalOrders { get; init; }
    public decimal TotalSpent { get; init; }
    public string Currency { get; init; } = "USD";
    public DateTime LastOrderDate { get; init; }
    public int SatisfactionScore { get; init; } // 1–5
    public string[] Notes { get; init; } = [];
}
