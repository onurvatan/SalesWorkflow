using SalesWorkflow.Data;
using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.Text.Json;

namespace SalesWorkflow.Tools;

public static class OrderStatusTool
{
    public static AIFunction Create(IOrderRepository orderRepo)
    {
        return AIFunctionFactory.Create(
            async ([Description("Order ID (e.g. ORD-003) or Customer ID (e.g. CUST-002) to retrieve order status for")] string query,
                   CancellationToken _) =>
            {
                // Try exact order ID first
                var byOrder = orderRepo.FindByOrderId(query);
                var orders = byOrder is not null
                    ? [byOrder]
                    : orderRepo.FindByCustomerId(query);

                if (orders.Count == 0)
                    return $"No orders found for '{query}'. Try a valid Order ID (e.g. ORD-001) or Customer ID (e.g. CUST-001).";

                var results = orders.Select(o => new
                {
                    orderId = o.OrderId,
                    customerId = o.CustomerId,
                    productSku = o.ProductSku,
                    productName = o.ProductName,
                    quantity = o.Quantity,
                    totalAmount = o.TotalAmount,
                    currency = o.Currency,
                    orderDate = o.OrderDate.ToString("yyyy-MM-dd"),
                    status = o.Status.ToString()
                });

                return JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = false });
            },
            name: "order_status",
            description: "Retrieve order status and details by Order ID (e.g. ORD-003) or Customer ID (e.g. CUST-002). Returns product, amount, date, and current fulfillment status.");
    }
}
