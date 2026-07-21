namespace Athena.Ingestion.Extraction;

/// <summary>
/// Detects table-like regions in a PDF and serialises each one to Markdown,
/// preserving row/column structure. Flattening a table into whitespace-joined
/// prose is treated as a failed extraction, per assignment §6.2.
/// </summary>
public interface ITableExtractor
{
    /// <summary>
    /// Extracts every detected table from the PDF at <paramref name="pdfPath"/>.
    /// </summary>
    /// <returns>One entry per detected table: its page number and Markdown source.</returns>
    IReadOnlyList<(int PageNumber, string MarkdownTable)> Extract(string pdfPath);
}