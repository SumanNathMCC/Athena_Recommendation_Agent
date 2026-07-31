namespace Athena.Recommendation;

public interface IInterestProfileStore
{
    /// <summary>
    /// profile &lt;- decay * profile + (1 - decay) * embed(latestQuery)
    /// Scoped per session — not a process-wide singleton keyed on nothing.
    /// </summary>
    Task<ReadOnlyMemory<float>> UpdateAsync(
        string sessionId,
        ReadOnlyMemory<float> queryVector,
        double decay = 0.8,
        CancellationToken ct = default);

    Task<ReadOnlyMemory<float>?> GetAsync(string sessionId, CancellationToken ct = default);

    Task<IReadOnlySet<string>> GetAlreadySurfacedAsync(string sessionId, CancellationToken ct = default);

    Task MarkSurfacedAsync(
        string sessionId,
        IEnumerable<string> docIds,
        CancellationToken ct = default);
}

/// <summary>
/// In-memory per-session interest profile and already-surfaced doc ids.
/// Register as scoped (Blazor circuit) or singleton for multi-session tests.
/// </summary>
public sealed class SessionInterestProfileStore : IInterestProfileStore
{
    private readonly Dictionary<string, SessionState> _sessions =
        new(StringComparer.Ordinal);

    private readonly object _gate = new();

    public Task<ReadOnlyMemory<float>> UpdateAsync(
        string sessionId,
        ReadOnlyMemory<float> queryVector,
        double decay = 0.8,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        if (queryVector.Length == 0)
        {
            throw new ArgumentException("Query vector must be non-empty.", nameof(queryVector));
        }

        lock (_gate)
        {
            var state = GetOrCreate(sessionId);
            state.Profile = VectorMath.Blend(state.Profile, queryVector, decay);
            return Task.FromResult(state.Profile.Value);
        }
    }

    public Task<ReadOnlyMemory<float>?> GetAsync(string sessionId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        lock (_gate)
        {
            if (_sessions.TryGetValue(sessionId, out var state) && state.Profile is { Length: > 0 } profile)
            {
                return Task.FromResult<ReadOnlyMemory<float>?>(profile);
            }

            return Task.FromResult<ReadOnlyMemory<float>?>(null);
        }
    }

    public Task<IReadOnlySet<string>> GetAlreadySurfacedAsync(
        string sessionId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        lock (_gate)
        {
            if (!_sessions.TryGetValue(sessionId, out var state))
            {
                return Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            }

            return Task.FromResult<IReadOnlySet<string>>(
                new HashSet<string>(state.Surfaced, StringComparer.OrdinalIgnoreCase));
        }
    }

    public Task MarkSurfacedAsync(
        string sessionId,
        IEnumerable<string> docIds,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(docIds);

        lock (_gate)
        {
            var state = GetOrCreate(sessionId);
            foreach (var id in docIds)
            {
                if (!string.IsNullOrWhiteSpace(id))
                {
                    state.Surfaced.Add(id.Trim());
                }
            }
        }

        return Task.CompletedTask;
    }

    private SessionState GetOrCreate(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var state))
        {
            state = new SessionState();
            _sessions[sessionId] = state;
        }

        return state;
    }

    private sealed class SessionState
    {
        public ReadOnlyMemory<float>? Profile { get; set; }

        public HashSet<string> Surfaced { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
