using Athena.Core.Records;
using Athena.Retrieval;

namespace Athena.Recommendation;

public interface IRecommendationScorer
{
    /// <summary>
    /// final = w1 * docSim + w2 * normalise(chunkAggregate) + w3 * recency
    /// </summary>
    IReadOnlyList<Recommendation> Score(
        string query,
        ReadOnlyMemory<float> queryVector,
        IReadOnlyList<DocRecord> candidates,
        IReadOnlyList<Passage> chunkHits);
}

/// <summary>
/// Blends document similarity, length-normalised chunk-hit strength, and recency.
/// </summary>
public sealed class BlendedRecommendationScorer : IRecommendationScorer
{
    private readonly RecommendationOptions _options;

    public BlendedRecommendationScorer(RecommendationOptions? options = null)
    {
        _options = options ?? new RecommendationOptions();
    }

    public IReadOnlyList<Recommendation> Score(
        string query,
        ReadOnlyMemory<float> queryVector,
        IReadOnlyList<DocRecord> candidates,
        IReadOnlyList<Passage> chunkHits)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(chunkHits);

        if (candidates.Count == 0)
        {
            return Array.Empty<Recommendation>();
        }

        var now = DateTimeOffset.UtcNow;
        var aggregates = BuildChunkAggregates(chunkHits);

        // Length-normalise first, then min-max so long docs do not dominate purely by hit count.
        var lengthAdjusted = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var doc in candidates)
        {
            aggregates.TryGetValue(doc.DocId, out var agg);
            var lengthFactor = Math.Log(1 + Math.Max(1, doc.PageCount));
            lengthAdjusted[doc.DocId] = agg.Score / lengthFactor;
        }

        var maxAdjusted = lengthAdjusted.Count == 0 ? 0d : lengthAdjusted.Values.DefaultIfEmpty(0).Max();

        var scored = new List<(DocRecord Doc, double Score, double DocSim, double Chunk, double Recency, int Hits)>();

        foreach (var doc in candidates)
        {
            var docSim = queryVector.Length > 0 && doc.Embedding.Length > 0
                ? VectorMath.CosineSimilarity(queryVector, doc.Embedding)
                : 0d;

            aggregates.TryGetValue(doc.DocId, out var agg);
            var hits = agg.Hits;
            var chunkNorm = maxAdjusted <= 0
                ? 0d
                : Math.Clamp(lengthAdjusted[doc.DocId] / maxAdjusted, 0d, 1d);

            var ageDays = Math.Max(0d, (now - doc.PublishedOn).TotalDays);
            var recency = Math.Exp(-ageDays / Math.Max(1d, _options.RecencyTauDays));

            var final =
                _options.DocSimWeight * docSim +
                _options.ChunkHitWeight * chunkNorm +
                _options.RecencyWeight * recency;

            scored.Add((doc, final, docSim, chunkNorm, recency, hits));
        }

        return scored
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.Doc.DocId, StringComparer.OrdinalIgnoreCase)
            .Select(s => new Recommendation(
                s.Doc.DocId,
                s.Doc.Title,
                BuildReason(query, s.Doc, s.DocSim, s.Chunk, s.Recency, s.Hits),
                s.Doc.Topics?.ToList() ?? [],
                s.Score))
            .ToList();
    }

    private static Dictionary<string, (double Score, int Hits)> BuildChunkAggregates(
        IReadOnlyList<Passage> chunkHits)
    {
        var map = new Dictionary<string, (double Score, int Hits)>(StringComparer.OrdinalIgnoreCase);
        for (var rank = 0; rank < chunkHits.Count; rank++)
        {
            var hit = chunkHits[rank];
            var contrib = 1d / (rank + 1);
            if (map.TryGetValue(hit.DocId, out var existing))
            {
                map[hit.DocId] = (existing.Score + contrib, existing.Hits + 1);
            }
            else
            {
                map[hit.DocId] = (contrib, 1);
            }
        }

        return map;
    }

    private static string BuildReason(
        string query,
        DocRecord doc,
        double docSim,
        double chunkNorm,
        double recency,
        int hits)
    {
        var topics = doc.Topics is { Count: > 0 }
            ? string.Join(", ", doc.Topics.Take(3))
            : "general coverage";

        var hitPhrase = hits <= 0
            ? "few direct passage hits"
            : hits == 1
                ? "1 strong passage hit"
                : $"{hits} passage hits";

        var focus = string.IsNullOrWhiteSpace(query) ? "your interest" : $"“{Trim(query, 48)}”";

        return $"Matches {focus} via doc similarity {docSim:F2}, {hitPhrase} " +
               $"(chunk strength {chunkNorm:F2}), recency {recency:F2}; topics: {topics}.";
    }

    private static string Trim(string value, int max)
    {
        var t = value.Trim();
        return t.Length <= max ? t : t[..max] + "…";
    }
}
