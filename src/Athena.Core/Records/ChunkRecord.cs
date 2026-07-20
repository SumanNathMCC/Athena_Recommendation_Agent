using Microsoft.Extensions.VectorData;

namespace Athena.Core.Records;

/// <summary>
/// Passage-level vector record (~2,500–5,000 per corpus).
/// </summary>
public sealed class ChunkRecord
{
    [VectorStoreKey]
    public string ChunkId { get; set; } = string.Empty;

    [VectorStoreData(IsFullTextIndexed = true)]
    public string Text { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public string DocId { get; set; } = string.Empty;

    [VectorStoreData]
    public string Title { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public int PageNumber { get; set; }

    [VectorStoreData]
    public string Section { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public string Cluster { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public DateTimeOffset PublishedOn { get; set; }

    [VectorStoreData]
    public ChunkKind Kind { get; set; }

    [VectorStoreVector(
        Dimensions: 1536,
        DistanceFunction = DistanceFunction.CosineSimilarity)]
    public ReadOnlyMemory<float> Embedding { get; set; }
}
