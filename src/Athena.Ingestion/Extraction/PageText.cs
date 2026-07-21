namespace Athena.Ingestion.Extraction;

/// <summary>
/// Extracted text for a single PDF page, along with an extraction confidence signal.
/// </summary>
/// <param name="PageNumber">1-based page number within the source PDF.</param>
/// <param name="Text">The extracted text content for this page.</param>
/// <param name="MeanConfidence">
/// Mean extraction confidence for the page. For PdfPig this can be 1.0 (native text),
/// for Tesseract this is the OCR engine's mean word confidence (0.0–1.0).
/// </param>
public readonly record struct PageText(int PageNumber, string Text, float MeanConfidence);