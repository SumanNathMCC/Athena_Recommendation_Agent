namespace Athena.Ingestion.Extraction;

/// <summary>
/// Extracts per-page text from a PDF file. Implementations may be native-text-based
/// (PdfPig) or OCR-based (Tesseract via Docnet.Core rasterisation), or route between
/// the two per page (Hybrid).
/// </summary>
public interface IPdfTextExtractor
{
    /// <summary>
    /// Extracts text for every page in the PDF at <paramref name="pdfPath"/>.
    /// </summary>
    /// <param name="pdfPath">Absolute path to the PDF file on disk.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One <see cref="PageText"/> per page, ordered by page number.</returns>
    Task<IReadOnlyList<PageText>> ExtractAsync(string pdfPath, CancellationToken ct = default);
}