using UglyToad.PdfPig;

namespace Athena.Ingestion.Extraction;

public sealed class PdfPigTextExtractor : IPdfTextExtractor
{
    public async Task<IReadOnlyList<PageText>> ExtractAsync(string pdfPath, CancellationToken ct = default)
    {
        var pageTexts = new List<PageText>();
        using (var document = PdfDocument.Open(pdfPath))
        {
            foreach (var page in document.GetPages())
            {
                ct.ThrowIfCancellationRequested();
                
                var words = page.GetWords();
                var text = string.Join(" ", words.Select(w => w.Text));

                // Setting mean confidence to 1.0f as PdfPig does not provide confidence scores
                pageTexts.Add(new PageText(page.Number, text, 1.0f));
            }
        }
        return await Task.FromResult(pageTexts);
    }
}