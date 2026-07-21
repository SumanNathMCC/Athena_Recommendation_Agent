namespace Athena.Ingestion.Extraction;

public sealed class HybridTextExtractor : IPdfTextExtractor
{
    private readonly PdfPigTextExtractor _pdfPigExtractor;
    private readonly TesseractOcrExtractor _tesseractExtractor;
    private readonly int _minCharsPerPage;

    public HybridTextExtractor(PdfPigTextExtractor pdfPigExtractor, TesseractOcrExtractor tesseractExtractor, int minCharsPerPage = 50)
    {
        _pdfPigExtractor = pdfPigExtractor;
        _tesseractExtractor = tesseractExtractor;
        _minCharsPerPage = minCharsPerPage;
    }

    public async Task<IReadOnlyList<PageText>> ExtractAsync(string pdfPath, CancellationToken ct = default)
    {
        var pdfTextPages = await _pdfPigExtractor.ExtractAsync(pdfPath, ct);
        var pagesToReprocessInOCR = pdfTextPages.Where(p => p.Text.Length < _minCharsPerPage).Select(p => p.PageNumber).ToList();

        if (!pagesToReprocessInOCR.Any())
        {
            return pdfTextPages;
        }

        var ocrByPages = await _tesseractExtractor.ExtractAsync(pdfPath, ct);
        var ocrByPageNumber = ocrByPages.ToDictionary(p => p.PageNumber);

        var combinedPages = new List<PageText>();

        foreach (var page in pdfTextPages)
        {
            ct.ThrowIfCancellationRequested();

            if (pagesToReprocessInOCR.Contains(page.PageNumber) &&
                ocrByPageNumber.TryGetValue(page.PageNumber, out var ocrPage))
            {
                combinedPages.Add(ocrPage);
            }
            else
            {
                combinedPages.Add(page);
            }
        }

        return combinedPages;
    }
}