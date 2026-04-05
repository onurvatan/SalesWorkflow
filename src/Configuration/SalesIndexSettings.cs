namespace SalesWorkflow.Configuration;

public class SalesIndexSettings
{
    public string? CatalogIndexName { get; init; }
    public string? SemanticConfigName { get; init; }
    public string VectorFieldName { get; init; } = "contentVector";
    public int VectorDimensions { get; init; } = 1536;
}
