namespace Athena.Retrieval;

public sealed class RetrievedContextAccessor : IRetrievedContextAccessor
{
    private IReadOnlyList<Passage> _passages = Array.Empty<Passage>();

    public IReadOnlyList<Passage> LastPassages => _passages;

    public string? LastQuery { get; private set; }

    public void Set(IReadOnlyList<Passage> passages, string? query = null)
    {
        _passages = passages is null || passages.Count == 0
            ? Array.Empty<Passage>()
            : passages.ToList();
        LastQuery = query;
    }

    public void Clear()
    {
        _passages = Array.Empty<Passage>();
        LastQuery = null;
    }
}
