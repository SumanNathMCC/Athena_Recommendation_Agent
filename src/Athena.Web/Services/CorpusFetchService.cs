using Athena.Core.Corpus;
using Athena.Ingestion.Fetch;

namespace Athena.Web.Services;

public interface ICorpusFetchService
{
    Task<FetchResult> FetchAllAsync(bool force = false, CancellationToken ct = default);
}

public sealed class CorpusFetchService : ICorpusFetchService
{
    private readonly ICorpusFetcher _fetcher;
    private readonly IWebHostEnvironment _environment;

    public CorpusFetchService(ICorpusFetcher fetcher, IWebHostEnvironment environment)
    {
        _fetcher = fetcher;
        _environment = environment;
    }

    public Task<FetchResult> FetchAllAsync(bool force = false, CancellationToken ct = default)
    {
        var repoRoot = RepoRootLocator.Find(_environment.ContentRootPath);
        return _fetcher.FetchAsync(repoRoot, force, docId: null, ct);
    }
}
