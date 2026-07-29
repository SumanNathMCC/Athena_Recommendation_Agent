using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using SkKernel = Microsoft.SemanticKernel.Kernel;

namespace Athena.Ingestion.Orchestration;

/// <summary>
/// Builds the shared Semantic Kernel used for ingestion orchestration (summaries + embeddings).
/// </summary>
public static class AthenaKernelFactory
{
    public static SkKernel Build(AzureFoundryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var builder = SkKernel.CreateBuilder();

        if (options.IsConfigured)
        {
#pragma warning disable SKEXP0010
            builder.AddAzureOpenAIChatCompletion(
                deploymentName: options.ChatDeployment,
                endpoint: options.Endpoint,
                apiKey: options.ApiKey);

            builder.AddAzureOpenAIEmbeddingGenerator(
                deploymentName: options.EmbeddingDeployment,
                endpoint: options.Endpoint,
                apiKey: options.ApiKey,
                modelId: options.EmbeddingDeployment,
                dimensions: options.EmbeddingDimensions);
#pragma warning restore SKEXP0010
        }

        return builder.Build();
    }
}
