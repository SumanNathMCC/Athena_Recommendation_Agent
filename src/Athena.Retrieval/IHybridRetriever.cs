namespace Athena.Retrieval;

public interface IHybridRetriever
{
    Task<IReadOnlyList<Passage>> RetrieveAsync(
        string query,
        int topK = 6,
        string? docId = null,
        CancellationToken ct = default);
}
