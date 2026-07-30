namespace Athena.Retrieval;

/// <summary>
/// Hybrid pipeline: dense top-20 + lexical top-20 → RRF → top-10 → LLM rerank → topK.
/// </summary>
public sealed class HybridRetriever : IHybridRetriever
{
    public const int DenseTopK = 20;
    public const int LexicalTopK = 20;
    public const int FusionTopN = 10;
    public const int DefaultTopK = 6;

    private readonly IDenseRetriever _dense;
    private readonly ILexicalRetriever _lexical;
    private readonly IReranker _reranker;

    public HybridRetriever(
        IDenseRetriever dense,
        ILexicalRetriever lexical,
        IReranker reranker)
    {
        _dense = dense;
        _lexical = lexical;
        _reranker = reranker;
    }

    public async Task<IReadOnlyList<Passage>> RetrieveAsync(
        string query,
        int topK = DefaultTopK,
        string? docId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<Passage>();
        }

        if (topK <= 0)
        {
            topK = DefaultTopK;
        }

        var denseTask = _dense.SearchAsync(query, DenseTopK, docId, ct);
        var lexicalTask = _lexical.SearchAsync(query, LexicalTopK, docId, ct);
        await Task.WhenAll(denseTask, lexicalTask).ConfigureAwait(false);

        var fused = ReciprocalRankFusion.Fuse(
            [denseTask.Result, lexicalTask.Result],
            FusionTopN,
            ReciprocalRankFusion.DefaultK);

        if (fused.Count == 0)
        {
            return Array.Empty<Passage>();
        }

        return await _reranker.RerankAsync(query, fused, topK, ct).ConfigureAwait(false);
    }
}
