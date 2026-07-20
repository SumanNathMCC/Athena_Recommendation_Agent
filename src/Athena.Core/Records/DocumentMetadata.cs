namespace Athena.Core.Records;

/// <summary>
/// Static document facts carried from the corpus manifest into extraction and chunking.
/// </summary>
public sealed class DocumentMetadata
{
    public string DocId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    /// <summary>Corpus cluster: A, B, C, or D.</summary>
    public string Cluster { get; init; } = string.Empty;

    public DateOnly PublishedOn { get; init; }

    public string? LineageGroup { get; init; }

    public string? LineageRole { get; init; }

    /// <summary>Absolute path to the PDF on disk.</summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>Original download URL from <c>manifest.json</c>, when applicable.</summary>
    public string? SourceUrl { get; init; }

    public int PageCountApprox { get; init; }

    public static DocumentMetadata FromManifest(
        Corpus.CorpusDocument document,
        string filePath)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        return new DocumentMetadata
        {
            DocId = document.DocId,
            Title = document.Title,
            Cluster = document.Cluster,
            PublishedOn = document.PublishedOn,
            LineageGroup = document.LineageGroup,
            LineageRole = document.LineageRole,
            FilePath = filePath,
            SourceUrl = document.Url,
            PageCountApprox = document.PageCountApprox
        };
    }

    public static DocumentMetadata FromManufactured(
        Corpus.ManufacturedDocument document,
        string filePath)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        return new DocumentMetadata
        {
            DocId = document.DocId,
            Title = document.Title,
            Cluster = document.Cluster,
            PublishedOn = document.PublishedOn,
            LineageGroup = document.LineageGroup,
            LineageRole = document.LineageRole,
            FilePath = filePath,
            SourceUrl = document.Generation?.SourceUrl,
            PageCountApprox = document.PageCountApprox
        };
    }
}
