using Azure;
using Azure.AI.DocumentIntelligence;

namespace Athena.Ingestion.Extraction;

public sealed class DocumentIntelligenceTextExtractor : IPdfTextExtractor
{
    private readonly DocumentIntelligenceClient _client;
    private readonly string _modelId;

    public DocumentIntelligenceTextExtractor(DocumentIntelligenceOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ApiKey);

        _client = new DocumentIntelligenceClient(
            new Uri(options.Endpoint),
            new AzureKeyCredential(options.ApiKey));

        _modelId = string.IsNullOrWhiteSpace(options.ModelId)
            ? "prebuilt-read"
            : options.ModelId;
    }

    public async Task<IReadOnlyList<PageText>> ExtractAsync(
        string pdfPath,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pdfPath);

        await using var stream = File.OpenRead(pdfPath);
        var bytes = await BinaryData.FromStreamAsync(stream, ct).ConfigureAwait(false);

        Operation<AnalyzeResult> operation = await _client.AnalyzeDocumentAsync(
            WaitUntil.Completed,
            _modelId,
            bytes,
            ct).ConfigureAwait(false);

        AnalyzeResult result = operation.Value;
        var pages = new List<PageText>();

        foreach (DocumentPage page in result.Pages)
        {
            ct.ThrowIfCancellationRequested();

            var text = string.Join(
                Environment.NewLine,
                page.Lines.Select(line => line.Content));

            var meanConfidence = page.Words.Count == 0
                ? 1.0f
                : (float)page.Words.Average(word => word.Confidence);

            pages.Add(new PageText(page.PageNumber, text.Trim(), meanConfidence));
        }

        return pages;
    }
}
