namespace Athena.Retrieval;

public interface IReranker
{
    Task<IReadOnlyList<Passage>> RerankAsync(
        string query,
        IReadOnlyList<Passage> candidates,
        int topK,
        CancellationToken ct = default);
}
