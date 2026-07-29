namespace Athena.Ingestion.Extraction;

public sealed class DocumentIntelligenceOptions
{
    public const string SectionName = "DocumentIntelligence";

    public string Endpoint { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Azure model id. <c>prebuilt-layout</c> extracts prose and tables in a single call.
    /// </summary>
    public string ModelId { get; set; } = "prebuilt-layout";
}
