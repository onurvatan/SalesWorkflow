using SalesWorkflow.Data;
using SalesWorkflow.Models;
using SalesWorkflow.Tools;
using Microsoft.Extensions.AI;
using Moq;
using System.Text.Json;
using Xunit;

namespace SalesWorkflow.Tests.Tools;

public class StockCheckToolTests
{
    private static AIFunction CreateTool(IProductRepository repo) =>
        StockCheckTool.Create(repo);

    [Fact]
    public void Create_ReturnsAIFunction_WithExpectedName()
    {
        var tool = CreateTool(new Mock<IProductRepository>().Object);
        Assert.Equal("stock_check", tool.Name);
    }

    [Fact]
    public void Create_ReturnsAIFunction_WithNonEmptyDescription()
    {
        var tool = CreateTool(new Mock<IProductRepository>().Object);
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
    }

    [Fact]
    public async Task InvokeAsync_StockQtyGte5_ReturnsInStock()
    {
        var product = BuildProduct(stockQty: 10);
        var repo = MockRepoReturning(product);
        var tool = CreateTool(repo);

        var result = await InvokeAsync(tool, "TEST-SKU");

        Assert.Contains("In Stock", result);
    }

    [Fact]
    public async Task InvokeAsync_StockQty1to4_ReturnsLowStock()
    {
        var product = BuildProduct(stockQty: 3);
        var repo = MockRepoReturning(product);
        var tool = CreateTool(repo);

        var result = await InvokeAsync(tool, "TEST-SKU");

        Assert.Contains("Low Stock", result);
    }

    [Fact]
    public async Task InvokeAsync_StockQty0_ReturnsOutOfStock()
    {
        var product = BuildProduct(stockQty: 0);
        var repo = MockRepoReturning(product);
        var tool = CreateTool(repo);

        var result = await InvokeAsync(tool, "TEST-SKU");

        Assert.Contains("Out of Stock", result);
    }

    [Fact]
    public async Task InvokeAsync_NoProductFound_ReturnsNotFoundMessage()
    {
        var mock = new Mock<IProductRepository>();
        mock.Setup(r => r.FindBySkuOrName(It.IsAny<string>()))
            .Returns([]);
        var tool = CreateTool(mock.Object);

        var result = await InvokeAsync(tool, "zzz-nonexistent");

        Assert.Contains("No products found", result);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static Product BuildProduct(int stockQty) => new()
    {
        Id = "test-001",
        Sku = "TEST-SKU",
        Name = "Test Product",
        Brand = "TestBrand",
        Category = "TestCategory",
        Description = "A test product.",
        Price = 99.99m,
        Currency = "USD",
        StockQuantity = stockQty,
        Tags = []
    };

    private static IProductRepository MockRepoReturning(Product product)
    {
        var mock = new Mock<IProductRepository>();
        mock.Setup(r => r.FindBySkuOrName(It.IsAny<string>()))
            .Returns([product]);
        return mock.Object;
    }

    private static async Task<string> InvokeAsync(AIFunction tool, string query)
    {
        var args = new AIFunctionArguments { ["query"] = query };
        var result = await tool.InvokeAsync(args);
        return result?.ToString() ?? string.Empty;
    }
}
