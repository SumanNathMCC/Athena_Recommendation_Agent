using Athena.Core.Records;
using Athena.Ingestion.Embeddings;

namespace Athena.Ingestion.DocVectors;

/// <summary>
/// Document vector = embedding of the LLM-generated summary.
/// </summary>
public sealed class SummaryStrategy : IDocumentVectorStrategy
{
    private readonly ICorpusEmbeddingService _embeddings;

    public SummaryStrategy(ICorpusEmbeddingService embeddings)
    {
        _embeddings = embeddings;
    }

    public string Name => "summary";

    public Task<ReadOnlyMemory<float>> BuildAsync(
        DocRecord doc,
        IReadOnlyList<ChunkRecord> chunks,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentException.ThrowIfNullOrWhiteSpace(doc.Summary);

        return _embeddings.EmbedAsync(doc.Summary, ct);
    }
}
