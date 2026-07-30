namespace Athena.Retrieval;

/// <summary>
/// Holds the passages from the latest hybrid retrieval for the current chat turn (UI + future grounding filters).
/// </summary>
public interface IRetrievedContextAccessor
{
    IReadOnlyList<Passage> LastPassages { get; }

    string? LastQuery { get; }

    void Set(IReadOnlyList<Passage> passages, string? query = null);

    void Clear();
}
