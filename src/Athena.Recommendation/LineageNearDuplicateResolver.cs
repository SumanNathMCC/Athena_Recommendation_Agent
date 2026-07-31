using Athena.Core.Records;

namespace Athena.Recommendation;

public interface INearDuplicateResolver
{
    /// <summary>
    /// Collapse near-identical documents to one representative before the list is returned.
    /// </summary>
    IReadOnlyList<DocRecord> Resolve(IReadOnlyList<DocRecord> ranked, DocRecord? seed = null);
}

/// <summary>
/// Lineage-aware dedup: within each <see cref="DocRecord.LineageGroup"/> keep only the newest
/// document (and never surface another member of the seed's lineage). Optional cosine ceiling
/// catches unlabeled near-twins.
/// Cost: lineage metadata is accurate but requires annotated pairs; the ceiling generalises
/// but can suppress two distinct documents that happen to be written similarly.
/// </summary>
public sealed class LineageNearDuplicateResolver : INearDuplicateResolver
{
    public const double DefaultSimilarityCeiling = 0.97;

    private readonly double _similarityCeiling;

    public LineageNearDuplicateResolver(double similarityCeiling = DefaultSimilarityCeiling)
    {
        _similarityCeiling = similarityCeiling;
    }

    public IReadOnlyList<DocRecord> Resolve(IReadOnlyList<DocRecord> ranked, DocRecord? seed = null)
    {
        if (ranked.Count == 0)
        {
            return Array.Empty<DocRecord>();
        }

        var seedId = seed?.DocId;
        var seedLineage = seed?.LineageGroup;

        // Newest wins within each lineage group (excluding the seed itself).
        var newestByLineage = new Dictionary<string, DocRecord>(StringComparer.OrdinalIgnoreCase);
        foreach (var doc in ranked)
        {
            if (seedId is not null &&
                string.Equals(doc.DocId, seedId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(doc.LineageGroup))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(seedLineage) &&
                string.Equals(doc.LineageGroup, seedLineage, StringComparison.OrdinalIgnoreCase))
            {
                // Other members of the seed's draft/final family never surface beside the seed.
                continue;
            }

            if (!newestByLineage.TryGetValue(doc.LineageGroup!, out var existing) ||
                doc.PublishedOn > existing.PublishedOn)
            {
                newestByLineage[doc.LineageGroup!] = doc;
            }
        }

        var lineageWinners = new HashSet<string>(
            newestByLineage.Values.Select(d => d.DocId),
            StringComparer.OrdinalIgnoreCase);

        var kept = new List<DocRecord>();
        foreach (var doc in ranked)
        {
            if (seedId is not null &&
                string.Equals(doc.DocId, seedId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(seedLineage) &&
                string.Equals(doc.LineageGroup, seedLineage, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(doc.LineageGroup) &&
                !lineageWinners.Contains(doc.DocId))
            {
                continue;
            }

            if (IsNearDuplicateOfKept(doc, kept, seed))
            {
                continue;
            }

            kept.Add(doc);
        }

        return kept;
    }

    private bool IsNearDuplicateOfKept(
        DocRecord candidate,
        IReadOnlyList<DocRecord> kept,
        DocRecord? seed)
    {
        if (candidate.Embedding.Length == 0 || _similarityCeiling <= 0)
        {
            return false;
        }

        if (seed is not null &&
            seed.Embedding.Length > 0 &&
            VectorMath.CosineSimilarity(candidate.Embedding, seed.Embedding) >= _similarityCeiling)
        {
            return true;
        }

        foreach (var existing in kept)
        {
            if (existing.Embedding.Length == 0)
            {
                continue;
            }

            if (VectorMath.CosineSimilarity(candidate.Embedding, existing.Embedding) >= _similarityCeiling)
            {
                return true;
            }
        }

        return false;
    }
}
