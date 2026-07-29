namespace Athena.Ingestion.Extraction;

/// <summary>
/// Extracts a PDF into a single Markdown document (prose + tables) via one Azure Document Intelligence call.
/// </summary>
public interface IDocumentMarkdownExtractor
{
    Task<string> ExtractToMarkdownAsync(
        string pdfPath,
        DocumentExtractionContext context,
        CancellationToken ct = default);
}

public sealed class DocumentExtractionContext
{
    public required string DocId { get; init; }

    public required string Title { get; init; }

    public string? SourcePdfPath { get; init; }
}
