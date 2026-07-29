using Microsoft.Extensions.VectorData;

namespace Athena.Core.Records;

/// <summary>
/// Document-level vector record (one per corpus PDF, ~16–22 total).
/// </summary>
public sealed class DocRecord
{
    public const int MaxSummaryWords = 150;
    public const int MinTopicCount = 3;
    public const int MaxTopicCount = 6;

    [VectorStoreRecordKey]
    public string DocId { get; set; } = string.Empty;

    [VectorStoreRecordData]
    public string Title { get; set; } = string.Empty;

    [VectorStoreRecordData(IsIndexed = true)]
    public string Cluster { get; set; } = string.Empty;

    [VectorStoreRecordData(IsIndexed = true)]
    public DateTimeOffset PublishedOn { get; set; }

    [VectorStoreRecordData]
    public int PageCount { get; set; }

    /// <summary>
    /// LLM-generated summary; must not exceed <see cref="MaxSummaryWords"/> words at ingestion time.
    /// </summary>
    [VectorStoreRecordData(IsFullTextIndexed = true)]
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Three to six LLM-extracted topic tags.
    /// </summary>
    [VectorStoreRecordData]
    public IList<string> Topics { get; set; } = [];

    /// <summary>
    /// Groups near-duplicate version-lineage documents (e.g. bcbs-op-resilience, ragas).
    /// Null when the document has no lineage pair.
    /// </summary>
    [VectorStoreRecordData(IsIndexed = true)]
    public string? LineageGroup { get; set; }

    [VectorStoreRecordVector(
        Dimensions: 1536,
        DistanceFunction = DistanceFunction.CosineSimilarity)]
    public ReadOnlyMemory<float> Embedding { get; set; }
}
