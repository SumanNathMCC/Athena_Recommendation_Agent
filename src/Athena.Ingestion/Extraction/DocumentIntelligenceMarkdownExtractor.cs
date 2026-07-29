using System.Text;
using Azure;
using Azure.AI.DocumentIntelligence;

namespace Athena.Ingestion.Extraction;

public sealed class DocumentIntelligenceMarkdownExtractor : IDocumentMarkdownExtractor
{
    private readonly DocumentIntelligenceClient _client;
    private readonly string _modelId;

    public DocumentIntelligenceMarkdownExtractor(DocumentIntelligenceOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ApiKey);

        _client = new DocumentIntelligenceClient(
            new Uri(options.Endpoint),
            new AzureKeyCredential(options.ApiKey));

        _modelId = string.IsNullOrWhiteSpace(options.ModelId)
            ? "prebuilt-layout"
            : options.ModelId;
    }

    public async Task<string> ExtractToMarkdownAsync(
        string pdfPath,
        DocumentExtractionContext context,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pdfPath);
        ArgumentNullException.ThrowIfNull(context);

        await using var stream = File.OpenRead(pdfPath);
        var bytes = await BinaryData.FromStreamAsync(stream, ct).ConfigureAwait(false);

        Operation<AnalyzeResult> operation = await _client.AnalyzeDocumentAsync(
            WaitUntil.Completed,
            _modelId,
            bytes,
            ct).ConfigureAwait(false);

        AnalyzeResult result = operation.Value;
        var body = LayoutMarkdownBuilder.BuildBody(result);

        return BuildDocument(context, _modelId, body);
    }

    internal static string BuildDocument(
        DocumentExtractionContext context,
        string modelId,
        string body)
    {
        var builder = new StringBuilder();
        builder.AppendLine("---");
        builder.AppendLine($"docId: {YamlValue(context.DocId)}");
        builder.AppendLine($"title: {YamlValue(context.Title)}");

        if (!string.IsNullOrWhiteSpace(context.SourcePdfPath))
        {
            builder.AppendLine($"sourcePdf: {YamlValue(context.SourcePdfPath)}");
        }

        builder.AppendLine($"extractedAt: {DateTimeOffset.UtcNow:O}");
        builder.AppendLine($"modelId: {YamlValue(modelId)}");
        builder.AppendLine("---");
        builder.AppendLine();

        if (!string.IsNullOrWhiteSpace(body))
        {
            builder.AppendLine(body);
        }

        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private static string YamlValue(string value) =>
        value.Contains(':', StringComparison.Ordinal) || value.Contains('\"', StringComparison.Ordinal)
            ? $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\""
            : value;
}
