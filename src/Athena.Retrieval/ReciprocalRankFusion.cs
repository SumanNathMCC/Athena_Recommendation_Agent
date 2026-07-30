namespace Athena.Retrieval;

/// <summary>
/// Reciprocal Rank Fusion: score = Σ 1/(k + rank) over ranked lists. Default k=60.
/// Do not average raw cosine and BM25 scores.
/// </summary>
public static class ReciprocalRankFusion
{
    public const int DefaultK = 60;

    public static IReadOnlyList<Passage> Fuse(
        IEnumerable<IReadOnlyList<Passage>> rankedLists,
        int topN,
        int k = DefaultK)
    {
        ArgumentNullException.ThrowIfNull(rankedLists);
        if (topN <= 0)
        {
            return Array.Empty<Passage>();
        }

        if (k <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(k), "RRF k must be positive.");
        }

        var scores = new Dictionary<string, (Passage Passage, double Score)>(StringComparer.Ordinal);
        foreach (var list in rankedLists)
        {
            if (list is null || list.Count == 0)
            {
                continue;
            }

            for (var rank = 1; rank <= list.Count; rank++)
            {
                var passage = list[rank - 1];
                var contribution = 1.0 / (k + rank);
                if (scores.TryGetValue(passage.ChunkId, out var existing))
                {
                    scores[passage.ChunkId] = (existing.Passage, existing.Score + contribution);
                }
                else
                {
                    scores[passage.ChunkId] = (passage, contribution);
                }
            }
        }

        return scores.Values
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Passage.ChunkId, StringComparer.Ordinal)
            .Take(topN)
            .Select(x => x.Passage with { Score = x.Score })
            .ToList();
    }
}
