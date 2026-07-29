using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using SkKernel = Microsoft.SemanticKernel.Kernel;

namespace Athena.Ingestion.Embeddings;

/// <summary>
/// Generates embeddings through Semantic Kernel's Azure OpenAI embedding generator.
/// </summary>
public sealed class SkCorpusEmbeddingService : ICorpusEmbeddingService
{
    private readonly SkKernel _kernel;
    private readonly AzureFoundryOptions _options;
    private IEmbeddingGenerator<string, Embedding<float>>? _embeddingGenerator;

    public SkCorpusEmbeddingService(SkKernel kernel, IOptions<AzureFoundryOptions> options)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        _kernel = kernel;
        _options = options.Value;
    }

    public async Task<ReadOnlyMemory<float>> EmbedAsync(string text, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var embedding = await GetEmbeddingGenerator()
            .GenerateAsync(text, cancellationToken: ct)
            .ConfigureAwait(false);

        return ValidateDimensions(embedding.Vector);
    }

    public async Task<IReadOnlyList<ReadOnlyMemory<float>>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(texts);

        if (texts.Count == 0)
        {
            return [];
        }

        var embeddings = await GetEmbeddingGenerator()
            .GenerateAsync(texts, cancellationToken: ct)
            .ConfigureAwait(false);

        return embeddings.Select(embedding => ValidateDimensions(embedding.Vector)).ToList();
    }

    private IEmbeddingGenerator<string, Embedding<float>> GetEmbeddingGenerator()
    {
        if (_embeddingGenerator is not null)
        {
            return _embeddingGenerator;
        }

        if (!_options.IsConfigured)
        {
            throw new InvalidOperationException(
                "AzureFoundry is not configured. Set Endpoint, ApiKey, ChatDeployment, and EmbeddingDeployment " +
                "(check appsettings.json is not overridden by empty values in appsettings.Development.json).");
        }

        try
        {
            _embeddingGenerator = _kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        }
        catch (KernelException ex)
        {
            throw new InvalidOperationException(
                "Azure OpenAI embedding generator is not registered on the Semantic Kernel. " +
                "Ensure AthenaKernelFactory calls AddAzureOpenAIEmbeddingGenerator and AzureFoundry is configured.",
                ex);
        }

        return _embeddingGenerator;
    }

    private ReadOnlyMemory<float> ValidateDimensions(ReadOnlyMemory<float> vector)
    {
        if (vector.Length != _options.EmbeddingDimensions)
        {
            throw new InvalidOperationException(
                $"Embedding dimension mismatch. Expected {_options.EmbeddingDimensions}, got {vector.Length}.");
        }

        return vector;
    }
}
