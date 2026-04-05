namespace SalesWorkflow.Models;

public class Product
{
    public string Id { get; init; } = default!;
    public string Sku { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string Brand { get; init; } = default!;
    public string Category { get; init; } = default!;
    public string Description { get; init; } = default!;
    public decimal Price { get; init; }
    public string Currency { get; init; } = "USD";
    public int StockQuantity { get; init; }
    public string[] Tags { get; init; } = [];
}
