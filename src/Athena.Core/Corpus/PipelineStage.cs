namespace Athena.Core.Corpus;

/// <summary>
/// Status of a single corpus pipeline stage for one document.
/// </summary>
public enum PipelineStageStatus
{
    NotStarted,
    Pending,
    InProgress,
    Succeeded,
    Skipped,
    Failed
}
