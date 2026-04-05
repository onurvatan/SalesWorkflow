using SalesWorkflow.Data;
using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.Text.Json;

namespace SalesWorkflow.Tools;

public static class StockCheckTool
{
    public static AIFunction Create(IProductRepository repository)
    {
        return AIFunctionFactory.Create(
            async ([Description("Product SKU, product name, or brand to look up stock and pricing for")] string query,
                   CancellationToken _) =>
            {
                var matches = repository.FindBySkuOrName(query);
                if (matches.Count == 0)
                    return $"No products found matching '{query}'. Try a different SKU, name, or brand.";

                var results = matches.Select(p => new
                {
                    sku = p.Sku,
                    name = p.Name,
                    price = p.Price,
                    currency = p.Currency,
                    stockQuantity = p.StockQuantity,
                    availability = p.StockQuantity == 0 ? "Out of Stock"
                                 : p.StockQuantity < 5 ? "Low Stock"
                                 : "In Stock"
                });

                return JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = false });
            },
            name: "stock_check",
            description: "Check real-time stock availability and pricing for a product by its SKU, name, or brand. Returns stock quantity and availability status.");
    }
}
