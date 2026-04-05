using System.ComponentModel.DataAnnotations;

namespace SalesWorkflow.Configuration;

public class FoundrySettings
{
    [Required]
    public string? Endpoint { get; init; }
    public string? Deployment { get; init; } = "gpt-4o";
    public string? EmbeddingDeployment { get; init; } = "text-embedding-3-small";
}
