using SalesWorkflow.Infrastructure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using Xunit;

namespace SalesWorkflow.Tests.Infrastructure;

public class AzureSearchHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenSearchSucceeds_ReturnsHealthy()
    {
        var mockClient = new Mock<SearchClient>();
        mockClient
            .Setup(c => c.SearchAsync<SearchDocument>(
                It.IsAny<string>(),
                It.IsAny<SearchOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Azure.Response<SearchResults<SearchDocument>>>());

        var sut = new AzureSearchHealthCheck(mockClient.Object);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("test", sut, null, null)
        };

        var result = await sut.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenSearchThrows_ReturnsUnhealthy()
    {
        var mockClient = new Mock<SearchClient>();
        mockClient
            .Setup(c => c.SearchAsync<SearchDocument>(
                It.IsAny<string>(),
                It.IsAny<SearchOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Search service unavailable"));

        var sut = new AzureSearchHealthCheck(mockClient.Object);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("test", sut, null, null)
        };

        var result = await sut.CheckHealthAsync(context, CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }
}
