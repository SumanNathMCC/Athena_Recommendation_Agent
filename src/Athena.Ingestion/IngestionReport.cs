namespace Athena.Ingestion;

public sealed record IngestionReport(
    int DocumentsProcessed,
    int ChunksWritten,
    int PagesOcrd,
    int TablesExtracted,
    IReadOnlyList<string> Warnings,
    TimeSpan Elapsed,
    string ChunkerName,
    string DocumentVectorStrategy);
