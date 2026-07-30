using System.Text;
using Athena.Core.Records;
using Athena.Ingestion.Embeddings;

namespace Athena.Ingestion.DocVectors;

/// <summary>
/// Document vector = embedding of Title + Topics + Summary concatenated.
/// </summary>
public sealed class CompositeStrategy : IDocumentVectorStrategy
{
    private readonly ICorpusEmbeddingService _embeddings;

    public CompositeStrategy(ICorpusEmbeddingService embeddings)
    {
        _embeddings = embeddings;
    }

    public string Name => "composite";

    public Task<ReadOnlyMemory<float>> BuildAsync(
        DocRecord doc,
        IReadOnlyList<ChunkRecord> chunks,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(doc);

        var builder = new StringBuilder();
        builder.AppendLine(doc.Title);

        if (doc.Topics is { Count: > 0 })
        {
            builder.AppendLine(string.Join(", ", doc.Topics));
        }

        builder.Append(doc.Summary);

        var text = builder.ToString().Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("CompositeStrategy requires title, topics, or summary text.");
        }

        return _embeddings.EmbedAsync(text, ct);
    }
}
