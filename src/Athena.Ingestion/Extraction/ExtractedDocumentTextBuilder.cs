using Athena.Ingestion.Extraction;

namespace Athena.Ingestion.Extraction;

public static class ExtractedDocumentTextBuilder
{
    public static string BuildPlainText(ExtractedMarkdownDocument document, int maxCharacters = 24_000)
    {
        ArgumentNullException.ThrowIfNull(document);

        var parts = document.Blocks
            .Select(block => block.Markdown.Trim())
            .Where(text => !string.IsNullOrWhiteSpace(text));

        var combined = string.Join("\n\n", parts);
        return combined.Length <= maxCharacters ? combined : combined[..maxCharacters];
    }
}
