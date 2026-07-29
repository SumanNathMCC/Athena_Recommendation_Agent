namespace Athena.Ingestion.Embeddings;

public interface ICorpusEmbeddingService
{
    Task<ReadOnlyMemory<float>> EmbedAsync(string text, CancellationToken ct = default);

    Task<IReadOnlyList<ReadOnlyMemory<float>>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken ct = default);
}
