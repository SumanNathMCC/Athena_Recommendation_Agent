using Athena.Core.Records;

namespace Athena.Ingestion.DocVectors;

public interface IDocumentVectorStrategy
{
    string Name { get; }

    Task<ReadOnlyMemory<float>> BuildAsync(
        DocRecord doc,
        IReadOnlyList<ChunkRecord> chunks,
        CancellationToken ct = default);
}
