using Athena.Core.Corpus;
using Athena.Ingestion.Chunking;
using Athena.Ingestion.Pipeline;

namespace Athena.Web.Services;

public interface ICorpusSetupService
{
    Task<IReadOnlyList<CorpusDocPipelineStatus>> GetStatusAsync(CancellationToken ct = default);

    Task<FetchResult> FetchAsync(bool force = false, CancellationToken ct = default);

    Task<CorpusPipelineOperationResult> ExtractAsync(bool force = false, CancellationToken ct = default);

    Task<CorpusPipelineOperationResult> InjectAsync(
        bool force = false,
        ChunkingStrategy strategy = ChunkingStrategy.SectionAware,
        CancellationToken ct = default);

    Task<CorpusFullPipelineResult> RunFullPipelineAsync(
        bool force = false,
        ChunkingStrategy strategy = ChunkingStrategy.SectionAware,
        CancellationToken ct = default);
}

public sealed class CorpusSetupService : ICorpusSetupService
{
    private readonly ICorpusPipelineService _pipeline;

    public CorpusSetupService(ICorpusPipelineService pipeline)
    {
        _pipeline = pipeline;
    }

    public Task<IReadOnlyList<CorpusDocPipelineStatus>> GetStatusAsync(CancellationToken ct = default) =>
        _pipeline.GetStatusAsync(ct);

    public Task<FetchResult> FetchAsync(bool force = false, CancellationToken ct = default) =>
        _pipeline.FetchAsync(force, ct);

    public Task<CorpusPipelineOperationResult> ExtractAsync(bool force = false, CancellationToken ct = default) =>
        _pipeline.ExtractAsync(force, ct);

    public Task<CorpusPipelineOperationResult> InjectAsync(
        bool force = false,
        ChunkingStrategy strategy = ChunkingStrategy.SectionAware,
        CancellationToken ct = default) =>
        _pipeline.InjectAsync(force, strategy, ct);

    public Task<CorpusFullPipelineResult> RunFullPipelineAsync(
        bool force = false,
        ChunkingStrategy strategy = ChunkingStrategy.SectionAware,
        CancellationToken ct = default) =>
        _pipeline.RunFullPipelineAsync(force, strategy, ct);
}
