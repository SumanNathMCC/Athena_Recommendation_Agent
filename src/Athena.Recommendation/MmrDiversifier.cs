using Athena.Core.Records;

namespace Athena.Recommendation;

public interface IDiversifier
{
    /// <summary>
    /// Maximal Marginal Relevance:
    /// next = argmax_d [ lambda * sim(d, seed) - (1 - lambda) * max_s sim(d, s) ]
    /// </summary>
    IReadOnlyList<DocRecord> Select(
        ReadOnlyMemory<float> seed,
        IReadOnlyList<DocRecord> candidates,
        int topK,
        double lambda = 0.7);
}

/// <summary>
/// MMR diversification over document embeddings.
/// </summary>
public sealed class MmrDiversifier : IDiversifier
{
    public IReadOnlyList<DocRecord> Select(
        ReadOnlyMemory<float> seed,
        IReadOnlyList<DocRecord> candidates,
        int topK,
        double lambda = 0.7)
    {
        if (topK <= 0 || candidates.Count == 0 || seed.Length == 0)
        {
            return Array.Empty<DocRecord>();
        }

        lambda = Math.Clamp(lambda, 0d, 1d);
        var remaining = candidates
            .Where(c => c.Embedding.Length > 0)
            .GroupBy(c => c.DocId, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        var selected = new List<DocRecord>(Math.Min(topK, remaining.Count));

        while (selected.Count < topK && remaining.Count > 0)
        {
            DocRecord? best = null;
            var bestScore = double.NegativeInfinity;

            foreach (var candidate in remaining)
            {
                var relevance = VectorMath.CosineSimilarity(seed, candidate.Embedding);
                var diversityPenalty = 0d;
                if (selected.Count > 0)
                {
                    diversityPenalty = selected.Max(s =>
                        VectorMath.CosineSimilarity(candidate.Embedding, s.Embedding));
                }

                var mmr = lambda * relevance - (1d - lambda) * diversityPenalty;
                if (mmr > bestScore)
                {
                    bestScore = mmr;
                    best = candidate;
                }
            }

            if (best is null)
            {
                break;
            }

            selected.Add(best);
            remaining.RemoveAll(c =>
                string.Equals(c.DocId, best.DocId, StringComparison.OrdinalIgnoreCase));
        }

        return selected;
    }
}
