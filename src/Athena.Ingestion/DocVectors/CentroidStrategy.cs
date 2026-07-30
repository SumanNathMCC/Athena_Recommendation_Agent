using Athena.Core.Records;

namespace Athena.Ingestion.DocVectors;

/// <summary>
/// Document vector = mean of the document's chunk embeddings.
/// </summary>
public sealed class CentroidStrategy : IDocumentVectorStrategy
{
    public string Name => "centroid";

    public Task<ReadOnlyMemory<float>> BuildAsync(
        DocRecord doc,
        IReadOnlyList<ChunkRecord> chunks,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(chunks);

        if (chunks.Count == 0)
        {
            throw new InvalidOperationException("CentroidStrategy requires at least one chunk embedding.");
        }

        ct.ThrowIfCancellationRequested();

        var dimensions = chunks[0].Embedding.Length;
        if (dimensions == 0)
        {
            throw new InvalidOperationException("Chunk embeddings are empty.");
        }

        var sums = new float[dimensions];
        var count = 0;

        foreach (var chunk in chunks)
        {
            var vector = chunk.Embedding.Span;
            if (vector.Length != dimensions)
            {
                throw new InvalidOperationException(
                    $"Chunk embedding dimension mismatch. Expected {dimensions}, got {vector.Length}.");
            }

            for (var i = 0; i < dimensions; i++)
            {
                sums[i] += vector[i];
            }

            count++;
        }

        for (var i = 0; i < dimensions; i++)
        {
            sums[i] /= count;
        }

        return Task.FromResult((ReadOnlyMemory<float>)sums);
    }
}
