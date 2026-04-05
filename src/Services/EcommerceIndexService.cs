using SalesWorkflow.Configuration;
using SalesWorkflow.Models;
using Azure;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;

namespace SalesWorkflow.Services;

public class EcommerceIndexService(
    SearchIndexClient indexClient,
    SearchClient catalogSearchClient,
    AzureOpenAIClient azureOpenAIClient,
    SalesIndexSettings salesSettings,
    FoundrySettings foundrySettings,
    ILogger<EcommerceIndexService> logger)
{
    private const string AlgorithmName = "product-hnsw";
    private const string VectorProfileName = "product-vector-profile";

    public async Task EnsureIndexExistsAsync(CancellationToken ct = default)
    {
        var indexName = salesSettings.CatalogIndexName!;
        try
        {
            await indexClient.GetIndexAsync(indexName, ct);
            logger.LogInformation("Catalog index '{IndexName}' already exists — skipping creation.", indexName);
            return;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            logger.LogInformation("Catalog index '{IndexName}' not found — creating.", indexName);
        }

        var vectorSearch = new VectorSearch();
        vectorSearch.Algorithms.Add(new HnswAlgorithmConfiguration(AlgorithmName));
        vectorSearch.Profiles.Add(new VectorSearchProfile(VectorProfileName, AlgorithmName));

        var index = new SearchIndex(indexName)
        {
            Fields =
            [
                new SimpleField("id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true },
                new SearchableField("sku") { IsFilterable = true },
                new SearchableField("name") { IsFilterable = true, IsSortable = true },
                new SearchableField("brand") { IsFilterable = true, IsFacetable = true },
                new SearchableField("category") { IsFilterable = true, IsFacetable = true },
                new SearchableField("description"),
                new SimpleField("price", SearchFieldDataType.Double) { IsFilterable = true, IsSortable = true },
                new SimpleField("currency", SearchFieldDataType.String),
                new SimpleField("stockQuantity", SearchFieldDataType.Int32) { IsFilterable = true, IsSortable = true },
                new SearchableField("tags", collection: true) { IsFilterable = true, IsFacetable = true },
                new SearchField(salesSettings.VectorFieldName, SearchFieldDataType.Collection(SearchFieldDataType.Single))
                {
                    IsSearchable = true,
                    VectorSearchDimensions = salesSettings.VectorDimensions,
                    VectorSearchProfileName = VectorProfileName
                }
            ],
            VectorSearch = vectorSearch
        };

        if (!string.IsNullOrEmpty(salesSettings.SemanticConfigName))
        {
            var semanticConfig = new SemanticConfiguration(
                salesSettings.SemanticConfigName,
                new SemanticPrioritizedFields
                {
                    TitleField = new SemanticField("name"),
                    ContentFields = { new SemanticField("description") },
                    KeywordsFields =
                    {
                        new SemanticField("tags"),
                        new SemanticField("brand"),
                        new SemanticField("category")
                    }
                });

            index.SemanticSearch = new SemanticSearch();
            index.SemanticSearch.Configurations.Add(semanticConfig);
        }

        await indexClient.CreateIndexAsync(index, ct);
        logger.LogInformation("Catalog index '{IndexName}' created.", indexName);
    }

    public async Task IndexProductsAsync(IEnumerable<Product> products, CancellationToken ct = default)
    {
        var embeddingClient = azureOpenAIClient.GetEmbeddingClient(foundrySettings.EmbeddingDeployment!);
        var documents = new List<SearchDocument>();

        foreach (var product in products)
        {
            var embeddingText = $"{product.Name} {product.Brand} {product.Category} {product.Description} {string.Join(" ", product.Tags)}";
            var response = await embeddingClient.GenerateEmbeddingAsync(embeddingText, cancellationToken: ct);
            var vector = response.Value.ToFloats().ToArray();

            documents.Add(new SearchDocument
            {
                ["id"] = product.Id,
                ["sku"] = product.Sku,
                ["name"] = product.Name,
                ["brand"] = product.Brand,
                ["category"] = product.Category,
                ["description"] = product.Description,
                ["price"] = (double)product.Price,
                ["currency"] = product.Currency,
                ["stockQuantity"] = product.StockQuantity,
                ["tags"] = product.Tags,
                [salesSettings.VectorFieldName] = vector
            });
        }

        var result = await catalogSearchClient.MergeOrUploadDocumentsAsync(documents, cancellationToken: ct);
        logger.LogInformation("Indexed {Count} products into catalog index '{IndexName}'.",
            documents.Count, salesSettings.CatalogIndexName);

        foreach (var r in result.Value.Results.Where(r => !r.Succeeded))
        {
            logger.LogWarning("Failed to index product '{Key}': {Error}", r.Key, r.ErrorMessage);
        }
    }
}
