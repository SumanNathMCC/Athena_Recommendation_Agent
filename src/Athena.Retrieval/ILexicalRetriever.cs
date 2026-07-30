namespace Athena.Retrieval;

public interface ILexicalRetriever
{
    Task<IReadOnlyList<Passage>> SearchAsync(
        string query,
        int topK = 20,
        string? docId = null,
        CancellationToken ct = default);
}
