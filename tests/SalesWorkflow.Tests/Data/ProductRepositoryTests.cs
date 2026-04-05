using SalesWorkflow.Data;
using Xunit;

namespace SalesWorkflow.Tests.Data;

public class ProductRepositoryTests
{
    private readonly IProductRepository _sut = new ProductRepository();

    [Fact]
    public void GetAll_Returns15Products()
    {
        var result = _sut.GetAll();
        Assert.Equal(15, result.Count);
    }

    [Fact]
    public void FindBySkuOrName_ExactSkuMatch_ReturnsSingleProduct()
    {
        var result = _sut.FindBySkuOrName("DELL-XPS15-2025");
        Assert.Single(result);
        Assert.Equal("DELL-XPS15-2025", result[0].Sku);
    }

    [Fact]
    public void FindBySkuOrName_PartialNameMatch_ReturnsList()
    {
        var result = _sut.FindBySkuOrName("MacBook");
        Assert.NotEmpty(result);
        Assert.All(result, p => Assert.Contains("MacBook", p.Name, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FindBySkuOrName_BrandMatch_ReturnsMultiple()
    {
        var result = _sut.FindBySkuOrName("Apple");
        Assert.True(result.Count >= 2);
        Assert.All(result, p => Assert.Equal("Apple", p.Brand, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void FindBySkuOrName_CaseInsensitive_ReturnsMatch()
    {
        var result = _sut.FindBySkuOrName("dell-xps15-2025");
        Assert.NotEmpty(result);
    }

    [Fact]
    public void FindBySkuOrName_NoMatch_ReturnsEmptyList()
    {
        var result = _sut.FindBySkuOrName("zzz-nonexistent-product");
        Assert.Empty(result);
    }
}
