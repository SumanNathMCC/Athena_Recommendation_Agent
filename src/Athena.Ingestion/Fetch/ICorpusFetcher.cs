using Athena.Core.Corpus;

namespace Athena.Ingestion.Fetch;

public interface ICorpusFetcher
{
    Task<FetchResult> FetchAsync(
        string repoRoot,
        bool force = false,
        string? docId = null,
        CancellationToken ct = default);
}
