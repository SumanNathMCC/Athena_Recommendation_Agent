using Athena.Core.Corpus;
using Athena.Ingestion.Chunking;
using Athena.Ingestion.DocVectors;
using Athena.Ingestion.Fetch;
using Athena.Ingestion.VectorStore;

namespace Athena.Ingestion.Pipeline;

public interface ICorpusPipelineService
{
    Task<IReadOnlyList<CorpusDocPipelineStatus>> GetStatusAsync(CancellationToken ct = default);

    Task<FetchResult> FetchAsync(bool force = false, CancellationToken ct = default);

    Task<CorpusPipelineOperationResult> ExtractAsync(bool force = false, CancellationToken ct = default);

    Task<CorpusPipelineOperationResult> InjectAsync(
        bool force = false,
        ChunkingStrategy strategy = ChunkingStrategy.SectionAware,
        DocumentVectorStrategyKind? documentVectorStrategy = null,
        CancellationToken ct = default);

    Task<CorpusFullPipelineResult> RunFullPipelineAsync(
        bool force = false,
        ChunkingStrategy strategy = ChunkingStrategy.SectionAware,
        DocumentVectorStrategyKind? documentVectorStrategy = null,
        CancellationToken ct = default);
}

public sealed record CorpusFullPipelineResult(
    FetchResult Fetch,
    CorpusPipelineOperationResult Extract,
    CorpusPipelineOperationResult Inject,
    IReadOnlyList<CorpusDocPipelineStatus> Documents)
{
    public bool IsSuccess =>
        Fetch.IsSuccess && Extract.IsSuccess && Inject.IsSuccess;
}

public sealed class CorpusPipelineService : ICorpusPipelineService
{
    private readonly ICorpusManifestReader _manifestReader;
    private readonly ICorpusFetcher _fetcher;
    private readonly ICorpusExtractor _extractor;
    private readonly ICorpusInjector _injector;
    private readonly ICorpusVectorIndexer _vectorIndexer;
    private readonly string _repoRoot;

    public CorpusPipelineService(
        ICorpusManifestReader manifestReader,
        ICorpusFetcher fetcher,
        ICorpusExtractor extractor,
        ICorpusInjector injector,
        ICorpusVectorIndexer vectorIndexer,
        string repoRoot)
    {
        _manifestReader = manifestReader;
        _fetcher = fetcher;
        _extractor = extractor;
        _injector = injector;
        _vectorIndexer = vectorIndexer;
        _repoRoot = repoRoot;
    }

    public async Task<IReadOnlyList<CorpusDocPipelineStatus>> GetStatusAsync(
        CancellationToken ct = default)
    {
        var manifest = await _manifestReader.ReadAsync(_repoRoot, ct);
        return await CorpusStageInspector.InspectAsync(manifest, _repoRoot, _vectorIndexer, ct);
    }

    public Task<FetchResult> FetchAsync(bool force = false, CancellationToken ct = default) =>
        _fetcher.FetchAsync(_repoRoot, force, docId: null, ct);

    public Task<CorpusPipelineOperationResult> ExtractAsync(
        bool force = false,
        CancellationToken ct = default) =>
        _extractor.ExtractAsync(_repoRoot, force, ct);

    public Task<CorpusPipelineOperationResult> InjectAsync(
        bool force = false,
        ChunkingStrategy strategy = ChunkingStrategy.SectionAware,
        DocumentVectorStrategyKind? documentVectorStrategy = null,
        CancellationToken ct = default) =>
        _injector.InjectAsync(_repoRoot, force, strategy, documentVectorStrategy, ct);

    public async Task<CorpusFullPipelineResult> RunFullPipelineAsync(
        bool force = false,
        ChunkingStrategy strategy = ChunkingStrategy.SectionAware,
        DocumentVectorStrategyKind? documentVectorStrategy = null,
        CancellationToken ct = default)
    {
        var fetch = await FetchAsync(force, ct);
        var extract = await ExtractAsync(force, ct);
        var inject = await InjectAsync(force, strategy, documentVectorStrategy, ct);
        var status = await GetStatusAsync(ct);

        return new CorpusFullPipelineResult(fetch, extract, inject, status);
    }
}
