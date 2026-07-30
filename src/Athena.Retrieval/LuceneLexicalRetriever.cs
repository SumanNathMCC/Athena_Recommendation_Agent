namespace Athena.Retrieval;

public sealed class LuceneLexicalRetriever : ILexicalRetriever
{
    private readonly LuceneChunkIndex _index;

    public LuceneLexicalRetriever(LuceneChunkIndex index)
    {
        _index = index;
    }

    public Task<IReadOnlyList<Passage>> SearchAsync(
        string query,
        int topK = 20,
        string? docId = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        IReadOnlyList<Passage> results = _index.Search(query, topK, docId);
        return Task.FromResult(results);
    }
}
