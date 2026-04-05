using SalesWorkflow.Configuration;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.Text;
using System.Text.Json;

namespace SalesWorkflow.Tools;

public static class CatalogSearchTool
{
    public static AIFunction Create(
        SearchClient catalogClient,
        AzureOpenAIClient azureOpenAIClient,
        SalesIndexSettings salesSettings,
        FoundrySettings foundrySettings)
    {
        return AIFunctionFactory.Create(
            async ([Description("Natural language search query describing desired product features, use case, budget, or specs")] string query,
                   CancellationToken ct) =>
            {
                // Generate query embedding for vector similarity search
                var embeddingClient = azureOpenAIClient.GetEmbeddingClient(foundrySettings.EmbeddingDeployment!);
                var embeddingResponse = await embeddingClient.GenerateEmbeddingAsync(query, cancellationToken: ct);
                var vector = embeddingResponse.Value.ToFloats().ToArray();

                var vectorQuery = new VectorizedQuery(vector)
                {
                    KNearestNeighborsCount = 5
                };
                vectorQuery.Fields.Add(salesSettings.VectorFieldName);

                var options = new SearchOptions { Size = 5 };
                options.VectorSearch = new VectorSearchOptions();
                options.VectorSearch.Queries.Add(vectorQuery);
                options.Select.Add("sku");
                options.Select.Add("name");
                options.Select.Add("brand");
                options.Select.Add("category");
                options.Select.Add("price");
                options.Select.Add("currency");
                options.Select.Add("description");
                options.Select.Add("stockQuantity");
                options.Select.Add("tags");

                if (!string.IsNullOrEmpty(salesSettings.SemanticConfigName))
                {
                    options.QueryType = SearchQueryType.Semantic;
                    options.SemanticSearch = new SemanticSearchOptions
                    {
                        SemanticConfigurationName = salesSettings.SemanticConfigName
                    };
                }

                var response = await catalogClient.SearchAsync<SearchDocument>(null, options, ct);

                var sb = new StringBuilder();
                await foreach (var result in response.Value.GetResultsAsync())
                {
                    sb.AppendLine(JsonSerializer.Serialize(result.Document));
                }

                return sb.Length > 0 ? sb.ToString() : "No products found matching that query.";
            },
            name: "catalog_search",
            description: "Search the electronics product catalog by features, use case, brand, price range, or specs. Returns matching products with name, brand, category, price, description, and tags.");
    }
}
