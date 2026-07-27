namespace Athena.Core.Corpus;

/// <summary>
/// Download, extraction, and injection status for one manifest document.
/// </summary>
public sealed record CorpusDocPipelineStatus(
    string DocId,
    string Title,
    string LocalFile,
    PipelineStageStatus Download,
    PipelineStageStatus Extract,
    PipelineStageStatus Inject,
    string? DownloadMessage = null,
    string? ExtractMessage = null,
    string? InjectMessage = null);

/// <summary>
/// Aggregate outcome for a corpus pipeline operation.
/// </summary>
public sealed record CorpusPipelineOperationResult(
    int Succeeded,
    int Skipped,
    int Failed,
    IReadOnlyList<CorpusDocPipelineStatus> Documents)
{
    public bool IsSuccess => Failed == 0;
}
