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

    [VectorStoreKey]
    public string DocId { get; set; } = string.Empty;

    [VectorStoreData]
    public string Title { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public string Cluster { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public DateTimeOffset PublishedOn { get; set; }

    [VectorStoreData]
    public int PageCount { get; set; }

    /// <summary>
    /// LLM-generated summary; must not exceed <see cref="MaxSummaryWords"/> words at ingestion time.
    /// </summary>
    [VectorStoreData(IsFullTextIndexed = true)]
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Three to six LLM-extracted topic tags.
    /// </summary>
    [VectorStoreData]
    public IList<string> Topics { get; set; } = [];

    /// <summary>
    /// Groups near-duplicate version-lineage documents (e.g. bcbs-op-resilience, ragas).
    /// Null when the document has no lineage pair.
    /// </summary>
    [VectorStoreData(IsIndexed = true)]
    public string? LineageGroup { get; set; }

    [VectorStoreVector(
        Dimensions: 1536,
        DistanceFunction = DistanceFunction.CosineSimilarity)]
    public ReadOnlyMemory<float> Embedding { get; set; }
}
