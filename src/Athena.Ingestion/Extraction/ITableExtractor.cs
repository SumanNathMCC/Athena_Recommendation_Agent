namespace Athena.Ingestion.Extraction;

/// <summary>
/// Legacy interface for standalone table extraction. Table extraction is handled by
/// <see cref="IDocumentMarkdownExtractor"/> via Azure <c>prebuilt-layout</c> in one call.
/// </summary>
public interface ITableExtractor
{
    /// <summary>
    /// Extracts every detected table from the PDF at <paramref name="pdfPath"/>.
    /// </summary>
    /// <returns>One entry per detected table: its page number and Markdown source.</returns>
    IReadOnlyList<(int PageNumber, string MarkdownTable)> Extract(string pdfPath);
}