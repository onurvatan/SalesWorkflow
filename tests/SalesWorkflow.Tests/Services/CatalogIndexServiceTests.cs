using SalesWorkflow.Configuration;
using SalesWorkflow.Services;
using Azure;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace SalesWorkflow.Tests.Services;

public class CatalogIndexServiceTests
{
    private static readonly SalesIndexSettings DefaultSalesSettings = new()
    {
        CatalogIndexName = "test-catalog",
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
    public async Task EnsureIndexExistsAsync_WhenIndexExists_SkipsCreateIndex()
    {
        var mockIndexClient = new Mock<SearchIndexClient>();
        mockIndexClient
            .Setup(c => c.GetIndexAsync("test-catalog", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response<SearchIndex>>());

        var sut = BuildSut(mockIndexClient.Object);

        await sut.EnsureIndexExistsAsync();

        mockIndexClient.Verify(
            c => c.CreateIndexAsync(It.IsAny<SearchIndex>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EnsureIndexExistsAsync_WhenIndexNotFound_CallsCreateIndex()
    {
        var mockIndexClient = new Mock<SearchIndexClient>();
        mockIndexClient
            .Setup(c => c.GetIndexAsync("test-catalog", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(404, "Index not found"));

        mockIndexClient
            .Setup(c => c.CreateIndexAsync(It.IsAny<SearchIndex>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response<SearchIndex>>());

        var sut = BuildSut(mockIndexClient.Object);

        await sut.EnsureIndexExistsAsync();

        mockIndexClient.Verify(
            c => c.CreateIndexAsync(It.IsAny<SearchIndex>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static CatalogIndexService BuildSut(SearchIndexClient indexClient) =>
        new(
            indexClient,
            new Mock<SearchClient>().Object,
            new Mock<AzureOpenAIClient>().Object,
            DefaultSalesSettings,
            DefaultFoundrySettings,
            NullLogger<CatalogIndexService>.Instance);
}
