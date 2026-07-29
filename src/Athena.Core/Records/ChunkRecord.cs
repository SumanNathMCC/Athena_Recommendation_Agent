using Microsoft.Extensions.VectorData;

namespace Athena.Core.Records;

/// <summary>
/// Passage-level vector record (~2,500–5,000 per corpus).
/// </summary>
public sealed class ChunkRecord
{
    [VectorStoreRecordKey]
    public string ChunkId { get; set; } = string.Empty;

    [VectorStoreRecordData(IsFullTextIndexed = true)]
    public string Text { get; set; } = string.Empty;

    [VectorStoreRecordData(IsIndexed = true)]
    public string DocId { get; set; } = string.Empty;

    [VectorStoreRecordData]
    public string Title { get; set; } = string.Empty;

    [VectorStoreRecordData(IsIndexed = true)]
    public int PageNumber { get; set; }

    [VectorStoreRecordData]
    public string Section { get; set; } = string.Empty;

    [VectorStoreRecordData(IsIndexed = true)]
    public string Cluster { get; set; } = string.Empty;

    [VectorStoreRecordData(IsIndexed = true)]
    public DateTimeOffset PublishedOn { get; set; }

    [VectorStoreRecordData]
    public ChunkKind Kind { get; set; }

    [VectorStoreRecordVector(
        Dimensions: 1536,
        DistanceFunction = DistanceFunction.CosineSimilarity)]
    public ReadOnlyMemory<float> Embedding { get; set; }
}
