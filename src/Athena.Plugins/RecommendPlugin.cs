using System.ComponentModel;
using System.Text;
using Athena.Recommendation;
using Microsoft.SemanticKernel;
using RecommendationHit = Athena.Recommendation.Recommendation;

namespace Athena.Plugins;

/// <summary>
/// Thin Semantic Kernel plugin over document-level recommendation (Part D).
/// </summary>
public sealed class RecommendPlugin
{
    private readonly IDocumentRecommender _recommender;
    private readonly ISessionContext _session;

    public RecommendPlugin(IDocumentRecommender recommender, ISessionContext session)
    {
        _recommender = recommender;
        _session = session;
    }

    [KernelFunction("more_like_this")]
    [Description(
        "Given one document the user has named (id or title), recommends other documents in the " +
        "library that are similar to it. Use when the user points at a specific document and asks " +
        "for related reading (e.g. 'papers like RAPTOR', 'more like B3'). " +
        "Do not use for factual questions about document contents.")]
    public async Task<string> MoreLikeThisAsync(
        [Description("Id or exact/partial title of the seed document (e.g. B4 or RAPTOR)")]
        string docId,
        [Description("How many documents to recommend")] int topK = 5,
        [Description("Relevance/diversity trade-off; 1.0 is pure relevance, 0.3 favours diversity")]
        double lambda = 0.7,
        CancellationToken ct = default)
    {
        var results = await _recommender
            .MoreLikeThisAsync(docId, topK, lambda, _session.SessionId, ct)
            .ConfigureAwait(false);

        return Format(results, heading: $"Similar to '{docId}'");
    }

    [KernelFunction("recommend_for_query")]
    [Description(
        "Recommends documents worth reading on a stated topic. Use when the user asks what to read " +
        "about something, rather than asking a factual question about it " +
        "(e.g. 'what should I read about evaluating RAG systems?'). " +
        "Do not use when the user wants an answer/explanation of a fact.")]
    public async Task<string> RecommendForQueryAsync(
        [Description("The topic the user wants reading on")] string query,
        [Description("How many documents to recommend")] int topK = 5,
        CancellationToken ct = default)
    {
        var results = await _recommender
            .RecommendForQueryAsync(query, topK, _session.SessionId, ct)
            .ConfigureAwait(false);

        return Format(results, heading: $"Reading on '{query}'");
    }

    [KernelFunction("recommend_for_user")]
    [Description(
        "Recommends documents based on what this user has asked about so far in the session. " +
        "Use for open-ended follow-ups such as 'what else should I read?' where no topic or " +
        "document is named. Do not use for factual Q&A.")]
    public async Task<string> RecommendForUserAsync(
        [Description("How many documents to recommend")] int topK = 5,
        CancellationToken ct = default)
    {
        var results = await _recommender
            .RecommendForUserAsync(topK, _session.SessionId, ct)
            .ConfigureAwait(false);

        if (results.Count == 0)
        {
            return "No session interest profile yet. Ask a corpus question or request reading on a topic first, then try again.";
        }

        return Format(results, heading: "Recommended for you");
    }

    private static string Format(IReadOnlyList<RecommendationHit> results, string heading)
    {
        if (results.Count == 0)
        {
            return "No recommendations available. Ensure documents are injected into the vector store.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"{heading} ({results.Count}):");
        for (var i = 0; i < results.Count; i++)
        {
            var r = results[i];
            var topics = r.Topics.Count > 0 ? string.Join(", ", r.Topics) : "n/a";
            sb.AppendLine($"{i + 1}. {r.Title} (docId={r.DocId}, score={r.Score:F3})");
            sb.AppendLine($"   Topics: {topics}");
            sb.AppendLine($"   Reason: {r.Reason}");
        }

        return sb.ToString().TrimEnd();
    }
}
