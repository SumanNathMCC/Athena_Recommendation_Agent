namespace Athena.Ingestion.Extraction;

public sealed class ExtractedDocument
{
    public string DocId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string SourcePdfPath { get; set; } = string.Empty;

    public DateTimeOffset ExtractedAt { get; set; }

    public List<ExtractedPage> Pages { get; set; } = [];
}

public sealed class ExtractedPage
{
    public int PageNumber { get; set; }

    public string Text { get; set; } = string.Empty;

    public float MeanConfidence { get; set; }
}
