namespace Athena.Ingestion.Extraction;

public sealed class DocumentIntelligenceOptions
{
    public const string SectionName = "DocumentIntelligence";

    public string Endpoint { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Azure model id. Use <c>prebuilt-read</c> for text/OCR or <c>prebuilt-layout</c> for layout + tables.
    /// </summary>
    public string ModelId { get; set; } = "prebuilt-read";
}
