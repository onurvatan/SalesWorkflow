using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SalesWorkflow.Infrastructure;

public class AzureSearchHealthCheck(SearchClient catalogClient) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct)
    {
        try
        {
            // Lightweight probe: 0 results, just confirms index is reachable
            await catalogClient.SearchAsync<SearchDocument>("*", new SearchOptions { Size = 0 }, ct);
            return HealthCheckResult.Healthy("Catalog index is reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Catalog index unreachable: {ex.Message}", ex);
        }
    }
}
