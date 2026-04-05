using SalesWorkflow.Configuration;
using SalesWorkflow.Tools;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Moq;
using Xunit;

namespace SalesWorkflow.Tests.Tools;

public class CatalogSearchToolTests
{
    private static readonly SalesIndexSettings DefaultSalesSettings = new()
    {
        CatalogIndexName = "test-index",
        VectorFieldName = "contentVector",
        VectorDimensions = 1536
    };

    private static readonly FoundrySettings DefaultFoundrySettings = new()
    {
        Endpoint = "https://test.openai.azure.com/",
        Deployment = "gpt-4o",
        EmbeddingDeployment = "text-embedding-3-small"
    };

    [Fact]
    public void Create_ReturnsAIFunction_WithExpectedName()
    {
        var tool = CatalogSearchTool.Create(
            new Mock<SearchClient>().Object,
            new Mock<AzureOpenAIClient>().Object,
            DefaultSalesSettings,
            DefaultFoundrySettings);

        Assert.Equal("catalog_search", tool.Name);
    }

    [Fact]
    public void Create_ReturnsAIFunction_WithNonEmptyDescription()
    {
        var tool = CatalogSearchTool.Create(
            new Mock<SearchClient>().Object,
            new Mock<AzureOpenAIClient>().Object,
            DefaultSalesSettings,
            DefaultFoundrySettings);

        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
    }
}
