namespace Athena.Ingestion.DocVectors;

public enum DocumentVectorStrategyKind
{
    Summary,
    Centroid,
    Composite
}

public sealed class DocumentVectorOptions
{
    public const string SectionName = "DocumentVector";

    /// <summary>Which document-level embedding strategy to use at ingest time.</summary>
    public DocumentVectorStrategyKind Strategy { get; set; } = DocumentVectorStrategyKind.Summary;
}

public interface IDocumentVectorStrategyFactory
{
    IDocumentVectorStrategy GetStrategy(DocumentVectorStrategyKind kind);

    IDocumentVectorStrategy GetDefaultStrategy();
}

public sealed class DocumentVectorStrategyFactory : IDocumentVectorStrategyFactory
{
    private readonly SummaryStrategy _summary;
    private readonly CentroidStrategy _centroid;
    private readonly CompositeStrategy _composite;
    private readonly DocumentVectorOptions _options;

    public DocumentVectorStrategyFactory(
        SummaryStrategy summary,
        CentroidStrategy centroid,
        CompositeStrategy composite,
        Microsoft.Extensions.Options.IOptions<DocumentVectorOptions> options)
    {
        _summary = summary;
        _centroid = centroid;
        _composite = composite;
        _options = options.Value;
    }

    public IDocumentVectorStrategy GetDefaultStrategy() => GetStrategy(_options.Strategy);

    public IDocumentVectorStrategy GetStrategy(DocumentVectorStrategyKind kind) =>
        kind switch
        {
            DocumentVectorStrategyKind.Centroid => _centroid,
            DocumentVectorStrategyKind.Composite => _composite,
            _ => _summary
        };
}
