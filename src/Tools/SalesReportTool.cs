using SalesWorkflow.Data;
using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.Text.Json;

namespace SalesWorkflow.Tools;

public static class SalesReportTool
{
    public static AIFunction Create(IOrderRepository orderRepo)
    {
        return AIFunctionFactory.Create(
            async ([Description("Optional date range filter description (e.g. 'last 30 days', 'Q1 2026'). Currently ignored — returns full summary from in-memory data.")] string? dateRange,
                   CancellationToken _) =>
            {
                var summary = orderRepo.GetSalesSummary();

                var result = new
                {
                    reportType = "Sales Summary",
                    dateRange = string.IsNullOrWhiteSpace(dateRange) ? "All time" : dateRange,
                    totalOrders = summary.TotalOrders,
                    totalRevenue = summary.TotalRevenue,
                    currency = summary.Currency,
                    ordersByStatus = summary.OrdersByStatus,
                    topProducts = summary.TopProducts.Select(p => new
                    {
                        sku = p.ProductSku,
                        name = p.ProductName,
                        units = p.UnitsSold,
                        revenue = p.Revenue
                    })
                };

                return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = false });
            },
            name: "sales_report",
            description: "Generate a sales summary report including total orders, revenue, order status breakdown, and top-selling products. Optionally filtered by a date range description.");
    }
}
