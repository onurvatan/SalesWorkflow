using SalesWorkflow.Models;

namespace SalesWorkflow.Data;

public record SatisfactionSummary(
    double AverageScore,
    int TotalCustomers,
    IReadOnlyDictionary<string, int> CustomersByTier,
    IReadOnlyDictionary<int, int> ScoreDistribution,
    IReadOnlyList<string> AtRiskCustomers);

public interface ICustomerRepository
{
    IReadOnlyList<Customer> GetAll();
    Customer? FindByCustomerId(string customerId);
    IReadOnlyList<Customer> FindByName(string name);
    SatisfactionSummary GetSatisfactionSummary();
}

public class CustomerRepository : ICustomerRepository
{
    private readonly IReadOnlyList<Customer> _customers =
    [
        new()
        {
            Id                = "customer-001",
            CustomerId        = "CUST-001",
            Name              = "Alice Johnson",
            Email             = "alice.johnson@example.com",
            Tier              = CustomerTier.Premium,
            TotalOrders       = 2,
            TotalSpent        = 2_998.99m,
            Currency          = "USD",
            LastOrderDate     = new DateTime(2026, 2, 5),
            SatisfactionScore = 5,
            Notes             = ["Prefers Apple products", "Fast shipping required"]
        },
        new()
        {
            Id                = "customer-002",
            CustomerId        = "CUST-002",
            Name              = "Bob Smith",
            Email             = "bob.smith@example.com",
            Tier              = CustomerTier.Standard,
            TotalOrders       = 2,
            TotalSpent        = 1_299.99m, // cancelled order excluded
            Currency          = "USD",
            LastOrderDate     = new DateTime(2026, 2, 14),
            SatisfactionScore = 3,
            Notes             = ["Had a cancelled order ORD-004", "May need follow-up"]
        },
        new()
        {
            Id                = "customer-003",
            CustomerId        = "CUST-003",
            Name              = "Carol White",
            Email             = "carol.white@example.com",
            Tier              = CustomerTier.VIP,
            TotalOrders       = 2,
            TotalSpent        = 4_598.98m,
            Currency          = "USD",
            LastOrderDate     = new DateTime(2026, 1, 30),
            SatisfactionScore = 4,
            Notes             = ["VIP priority handling", "Bought 2x Galaxy S25 Ultra"]
        },
        new()
        {
            Id                = "customer-004",
            CustomerId        = "CUST-004",
            Name              = "David Lee",
            Email             = "david.lee@example.com",
            Tier              = CustomerTier.Standard,
            TotalOrders       = 2,
            TotalSpent        = 1_399.99m, // pending order not yet paid
            Currency          = "USD",
            LastOrderDate     = new DateTime(2026, 3, 1),
            SatisfactionScore = 2,
            Notes             = ["Complained about delivery time", "Pending order ORD-007 not yet shipped"]
        },
        new()
        {
            Id                = "customer-005",
            CustomerId        = "CUST-005",
            Name              = "Eva Martinez",
            Email             = "eva.martinez@example.com",
            Tier              = CustomerTier.Premium,
            TotalOrders       = 2,
            TotalSpent        = 3_198.00m,
            Currency          = "USD",
            LastOrderDate     = new DateTime(2026, 3, 8),
            SatisfactionScore = 5,
            Notes             = ["Loyal repeat buyer", "Referred 3 new customers"]
        },
    ];

    public IReadOnlyList<Customer> GetAll() => _customers;

    public Customer? FindByCustomerId(string customerId) =>
        _customers.FirstOrDefault(c =>
            c.CustomerId.Equals(customerId, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<Customer> FindByName(string name) =>
        _customers
            .Where(c => c.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
            .ToList();

    public SatisfactionSummary GetSatisfactionSummary()
    {
        var all = _customers;

        var avg = all.Average(c => (double)c.SatisfactionScore);

        var byTier = all
            .GroupBy(c => c.Tier.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var scoreDistrib = all
            .GroupBy(c => c.SatisfactionScore)
            .ToDictionary(g => g.Key, g => g.Count());

        var atRisk = all
            .Where(c => c.SatisfactionScore <= 2)
            .Select(c => $"{c.Name} ({c.CustomerId}) — score {c.SatisfactionScore}")
            .ToList();

        return new SatisfactionSummary(
            AverageScore: Math.Round(avg, 2),
            TotalCustomers: all.Count,
            CustomersByTier: byTier,
            ScoreDistribution: scoreDistrib,
            AtRiskCustomers: atRisk);
    }
}
