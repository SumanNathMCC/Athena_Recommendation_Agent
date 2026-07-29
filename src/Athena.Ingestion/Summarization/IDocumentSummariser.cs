using Athena.Core.Records;

namespace Athena.Ingestion.Summarization;

public sealed class DocumentSummaryResult
{
    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<string> Topics { get; init; } = [];
}

public interface IDocumentSummariser
{
    Task<DocumentSummaryResult> SummarizeAsync(
        DocumentMetadata metadata,
        string documentText,
        CancellationToken ct = default);
}
