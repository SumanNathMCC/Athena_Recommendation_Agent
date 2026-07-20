namespace Athena.Core.Records;

/// <summary>
/// Describes how a chunk's text was produced during ingestion.
/// </summary>
public enum ChunkKind
{
    Prose,
    Table,
    OcrProse
}
