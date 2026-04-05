using SalesWorkflow.Data;
using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.Text.Json;

namespace SalesWorkflow.Tools;

public static class CustomerLookupTool
{
    public static AIFunction Create(ICustomerRepository customerRepo, IOrderRepository orderRepo)
    {
        return AIFunctionFactory.Create(
            async ([Description("Customer ID (e.g. CUST-001) or partial name to look up")] string query,
                   CancellationToken _) =>
            {
                // Try exact ID match first, then fall back to name search
                var customer = customerRepo.FindByCustomerId(query);
                var matches = customer is not null
                    ? [customer]
                    : customerRepo.FindByName(query);

                if (matches.Count == 0)
                    return $"No customer found matching '{query}'. Try a different customer ID or name.";

                var results = matches.Select(c =>
                {
                    var recentOrders = orderRepo
                        .FindByCustomerId(c.CustomerId)
                        .OrderByDescending(o => o.OrderDate)
                        .Take(5)
                        .Select(o => new
                        {
                            orderId = o.OrderId,
                            product = o.ProductName,
                            totalAmount = o.TotalAmount,
                            currency = o.Currency,
                            status = o.Status.ToString(),
                            orderDate = o.OrderDate.ToString("yyyy-MM-dd")
                        });

                    return new
                    {
                        customerId = c.CustomerId,
                        name = c.Name,
                        email = c.Email,
                        tier = c.Tier.ToString(),
                        totalOrders = c.TotalOrders,
                        totalSpent = c.TotalSpent,
                        currency = c.Currency,
                        satisfactionScore = c.SatisfactionScore,
                        notes = c.Notes,
                        recentOrders
                    };
                });

                return JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = false });
            },
            name: "customer_lookup",
            description: "Look up a customer profile and their recent orders by customer ID or name. Returns customer tier, satisfaction score, notes, and order history.");
    }
}
