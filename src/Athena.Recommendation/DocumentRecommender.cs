using Athena.Core.Records;
using Athena.Ingestion.Embeddings;
using Athena.Retrieval;
using Microsoft.Extensions.Options;

namespace Athena.Recommendation;

public interface IDocumentRecommender
{
    Task<IReadOnlyList<Recommendation>> MoreLikeThisAsync(
        string docIdOrTitle,
        int topK = 5,
        double lambda = 0.7,
        string? sessionId = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<Recommendation>> RecommendForQueryAsync(
        string query,
        int topK = 5,
        string? sessionId = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<Recommendation>> RecommendForUserAsync(
        int topK = 5,
        string? sessionId = null,
        CancellationToken ct = default);
}

/// <summary>
/// Document-level recommender: MMR + lineage dedup + blended scoring + session profile.
/// </summary>
public sealed class DocumentRecommender : IDocumentRecommender
{
    private readonly IDocumentCatalog _catalog;
    private readonly IDiversifier _diversifier;
    private readonly INearDuplicateResolver _dedup;
    private readonly IRecommendationScorer _scorer;
    private readonly IInterestProfileStore _profiles;
    private readonly IHybridRetriever _hybrid;
    private readonly ICorpusEmbeddingService _embeddings;
    private readonly RecommendationOptions _options;

    public DocumentRecommender(
        IDocumentCatalog catalog,
        IDiversifier diversifier,
        INearDuplicateResolver dedup,
        IRecommendationScorer scorer,
        IInterestProfileStore profiles,
        IHybridRetriever hybrid,
        ICorpusEmbeddingService embeddings,
        IOptions<RecommendationOptions>? options = null)
    {
        _catalog = catalog;
        _diversifier = diversifier;
        _dedup = dedup;
        _scorer = scorer;
        _profiles = profiles;
        _hybrid = hybrid;
        _embeddings = embeddings;
        _options = options?.Value ?? new RecommendationOptions();
    }

    public async Task<IReadOnlyList<Recommendation>> MoreLikeThisAsync(
        string docIdOrTitle,
        int topK = 5,
        double lambda = 0.7,
        string? sessionId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(docIdOrTitle))
        {
            return Array.Empty<Recommendation>();
        }

        topK = topK <= 0 ? 5 : topK;
        lambda = double.IsNaN(lambda) ? _options.DefaultLambda : Math.Clamp(lambda, 0d, 1d);

        var seed = await _catalog.FindByIdOrTitleAsync(docIdOrTitle, ct).ConfigureAwait(false);
        if (seed is null || seed.Embedding.Length == 0)
        {
            return Array.Empty<Recommendation>();
        }

        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            await _profiles.UpdateAsync(sessionId, seed.Embedding, ct: ct).ConfigureAwait(false);
        }

        var all = await _catalog.GetAllAsync(ct).ConfigureAwait(false);
        var candidates = all
            .Where(d => !string.Equals(d.DocId, seed.DocId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        candidates = _dedup.Resolve(candidates, seed).ToList();
        var selected = _diversifier.Select(seed.Embedding, candidates, topK, lambda);

        var results = selected
            .Select(doc => ToSimilarityRecommendation(seed, doc))
            .ToList();

        await MarkSurfacedAsync(sessionId, results, ct).ConfigureAwait(false);
        return results;
    }

    public async Task<IReadOnlyList<Recommendation>> RecommendForQueryAsync(
        string query,
        int topK = 5,
        string? sessionId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<Recommendation>();
        }

        topK = topK <= 0 ? 5 : topK;
        var queryVector = await _embeddings.EmbedAsync(query.Trim(), ct).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            await _profiles.UpdateAsync(sessionId, queryVector, ct: ct).ConfigureAwait(false);
        }

        var chunkHits = await _hybrid
            .RetrieveAsync(query.Trim(), _options.ChunkHitTopK, docId: null, ct)
            .ConfigureAwait(false);

        var all = await _catalog.GetAllAsync(ct).ConfigureAwait(false);
        var scored = _scorer.Score(query.Trim(), queryVector, all, chunkHits);
        var rankedDocs = await MapToDocsAsync(scored, ct).ConfigureAwait(false);
        rankedDocs = _dedup.Resolve(rankedDocs).ToList();

        var selected = _diversifier.Select(
            queryVector,
            rankedDocs,
            topK,
            _options.DefaultLambda);

        var byId = scored.ToDictionary(r => r.DocId, StringComparer.OrdinalIgnoreCase);
        var results = selected
            .Select(doc => byId.TryGetValue(doc.DocId, out var rec)
                ? rec
                : ToSimilarityRecommendation(queryVector, doc, query.Trim()))
            .ToList();

        await MarkSurfacedAsync(sessionId, results, ct).ConfigureAwait(false);
        return results;
    }

    public async Task<IReadOnlyList<Recommendation>> RecommendForUserAsync(
        int topK = 5,
        string? sessionId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return Array.Empty<Recommendation>();
        }

        topK = topK <= 0 ? 5 : topK;
        var profile = await _profiles.GetAsync(sessionId, ct).ConfigureAwait(false);
        if (profile is null || profile.Value.Length == 0)
        {
            return Array.Empty<Recommendation>();
        }

        var surfaced = await _profiles.GetAlreadySurfacedAsync(sessionId, ct).ConfigureAwait(false);
        var all = await _catalog.GetAllAsync(ct).ConfigureAwait(false);
        var candidates = all
            .Where(d => !surfaced.Contains(d.DocId))
            .ToList();

        candidates = _dedup.Resolve(candidates).ToList();

        // Score against empty chunk hits — profile is the main signal; use doc-sim + recency only.
        var scored = _scorer.Score(
            query: "session interest profile",
            queryVector: profile.Value,
            candidates,
            chunkHits: Array.Empty<Passage>());

        var rankedDocs = await MapToDocsAsync(scored, ct).ConfigureAwait(false);
        var selected = _diversifier.Select(profile.Value, rankedDocs, topK, _options.DefaultLambda);
        var byId = scored.ToDictionary(r => r.DocId, StringComparer.OrdinalIgnoreCase);

        var results = selected
            .Select(doc =>
            {
                if (byId.TryGetValue(doc.DocId, out var rec))
                {
                    return rec with
                    {
                        Reason = $"Aligned with your session interest profile (score {rec.Score:F2}); " +
                                 $"topics: {FormatTopics(doc)}."
                    };
                }

                return ToSimilarityRecommendation(profile.Value, doc, "your recent questions");
            })
            .ToList();

        await MarkSurfacedAsync(sessionId, results, ct).ConfigureAwait(false);
        return results;
    }

    private async Task<List<DocRecord>> MapToDocsAsync(
        IReadOnlyList<Recommendation> scored,
        CancellationToken ct)
    {
        var all = await _catalog.GetAllAsync(ct).ConfigureAwait(false);
        var map = all.ToDictionary(d => d.DocId, StringComparer.OrdinalIgnoreCase);
        var ordered = new List<DocRecord>();
        foreach (var rec in scored)
        {
            if (map.TryGetValue(rec.DocId, out var doc))
            {
                ordered.Add(doc);
            }
        }

        return ordered;
    }

    private async Task MarkSurfacedAsync(
        string? sessionId,
        IReadOnlyList<Recommendation> results,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || results.Count == 0)
        {
            return;
        }

        await _profiles.MarkSurfacedAsync(sessionId, results.Select(r => r.DocId), ct)
            .ConfigureAwait(false);
    }

    private static Recommendation ToSimilarityRecommendation(DocRecord seed, DocRecord doc)
    {
        var sim = VectorMath.CosineSimilarity(seed.Embedding, doc.Embedding);
        var shared = seed.Topics
            .Intersect(doc.Topics ?? [], StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        var topicBit = shared.Count > 0
            ? $"shared topics: {string.Join(", ", shared)}"
            : $"topics: {FormatTopics(doc)}";

        return new Recommendation(
            doc.DocId,
            doc.Title,
            $"Similar to {seed.DocId} ({seed.Title}) at cosine {sim:F2}; {topicBit}.",
            doc.Topics?.ToList() ?? [],
            sim);
    }

    private static Recommendation ToSimilarityRecommendation(
        ReadOnlyMemory<float> seedVector,
        DocRecord doc,
        string label)
    {
        var sim = VectorMath.CosineSimilarity(seedVector, doc.Embedding);
        return new Recommendation(
            doc.DocId,
            doc.Title,
            $"Close to {label} at cosine {sim:F2}; topics: {FormatTopics(doc)}.",
            doc.Topics?.ToList() ?? [],
            sim);
    }

    private static string FormatTopics(DocRecord doc) =>
        doc.Topics is { Count: > 0 }
            ? string.Join(", ", doc.Topics.Take(4))
            : "n/a";
}
