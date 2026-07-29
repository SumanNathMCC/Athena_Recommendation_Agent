namespace Athena.Ingestion;

/// <summary>
/// Azure AI Foundry / Azure OpenAI configuration for Semantic Kernel chat and embeddings.
/// Deployment names are the Foundry deployment names, not raw model ids.
/// </summary>
public sealed class AzureFoundryOptions
{
    public const string SectionName = "AzureFoundry";

    public string Endpoint { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Foundry chat deployment name (e.g. gpt-4o-mini).</summary>
    public string ChatDeployment { get; set; } = "gpt-4o-mini";

    /// <summary>Foundry embedding deployment name (e.g. text-embedding-3-small).</summary>
    public string EmbeddingDeployment { get; set; } = "text-embedding-3-small";

    public int EmbeddingDimensions { get; set; } = 1536;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Endpoint) &&
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(ChatDeployment) &&
        !string.IsNullOrWhiteSpace(EmbeddingDeployment);
}
