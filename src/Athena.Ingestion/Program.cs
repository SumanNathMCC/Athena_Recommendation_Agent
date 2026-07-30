using System.Net.Http.Headers;
using Athena.Ingestion;
using Athena.Ingestion.Chunking;
using Athena.Ingestion.DocVectors;
using Athena.Ingestion.Fetch;
using Athena.Ingestion.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using var cancellationSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationSource.Cancel();
};

return await RunAsync(args, cancellationSource.Token);

static async Task<int> RunAsync(string[] args, CancellationToken ct)
{
    if (args.Length == 0)
    {
        return PrintUsage();
    }

    try
    {
        return args[0].ToLowerInvariant() switch
        {
            "fetch" => await RunFetchCommandAsync(args[1..], ct),
            "ingest" => await RunIngestCommandAsync(args[1..], ct),
            _ => PrintUsage()
        };
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested)
    {
        Console.Error.WriteLine("Operation cancelled.");
        return 2;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"ERROR  {ex.Message}");
        return 1;
    }
}

static async Task<int> RunFetchCommandAsync(string[] args, CancellationToken ct)
{
    var options = ParseFetchOptions(args);
    var repoRoot = RepoRootLocator.Find();

    using var httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromMinutes(5)
    };

    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Athena-CorpusFetcher/1.0 (educational assignment)");
    httpClient.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("application/pdf"));

    ICorpusManifestReader manifestReader = new CorpusManifestReader();
    ICorpusFetcher fetcher = new CorpusFetcher(manifestReader, httpClient);

    var result = await fetcher.FetchAsync(
        repoRoot,
        options.Force,
        options.DocId,
        ct);

    Console.WriteLine();
    Console.WriteLine(
        $"Done. Downloaded={result.Downloaded}, Skipped={result.Skipped}, Failed={result.Failed}");

    return result.IsSuccess ? 0 : 1;
}

static async Task<int> RunIngestCommandAsync(string[] args, CancellationToken ct)
{
    var options = ParseIngestOptions(args);
    var repoRoot = RepoRootLocator.Find();
    var webRoot = Path.Combine(repoRoot, "src", "Athena.Web");

    var configBuilder = new ConfigurationBuilder()
        .SetBasePath(webRoot)
        .AddJsonFile("appsettings.json", optional: false)
        .AddJsonFile("appsettings.Development.json", optional: true)
        .AddEnvironmentVariables();

    // Load Athena.Web user secrets if present (shared AzureFoundry keys).
    var secretsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Microsoft",
        "UserSecrets",
        "athena-recommendation-engine-web",
        "secrets.json");
    if (File.Exists(secretsPath))
    {
        configBuilder.AddJsonFile(secretsPath, optional: true);
    }

    var configuration = configBuilder.Build();

    var services = new ServiceCollection();
    services.AddLogging(builder => builder.AddSimpleConsole(o => o.SingleLine = true));
    services.AddAthenaIngestion(configuration);
    services.AddSingleton<ICorpusManifestReader, CorpusManifestReader>();

    using var provider = services.BuildServiceProvider();
    var pipeline = provider.GetRequiredService<IngestionPipeline>();

    var report = await pipeline.RunAsync(
        repoRoot,
        options.Force,
        options.ChunkingStrategy,
        options.DocumentVectorStrategy,
        ct);

    Console.WriteLine();
    Console.WriteLine(
        $"Ingest complete. Documents={report.DocumentsProcessed}, Chunks={report.ChunksWritten}, " +
        $"Tables={report.TablesExtracted}, OCRPages={report.PagesOcrd}, " +
        $"Chunker={report.ChunkerName}, DocVector={report.DocumentVectorStrategy}, Elapsed={report.Elapsed}");

    foreach (var warning in report.Warnings)
    {
        Console.WriteLine($"  WARN  {warning}");
    }

    return report.Warnings.Any(w => w.Contains(": missing", StringComparison.OrdinalIgnoreCase) ||
                                    w.Contains(": Failed", StringComparison.OrdinalIgnoreCase) ||
                                    w.Contains("chunker produced", StringComparison.OrdinalIgnoreCase))
        && report.DocumentsProcessed == 0
            ? 1
            : 0;
}

static FetchOptions ParseFetchOptions(string[] args)
{
    var force = false;
    string? docId = null;

    for (var index = 0; index < args.Length; index++)
    {
        var argument = args[index];

        if (argument.Equals("--force", StringComparison.OrdinalIgnoreCase))
        {
            force = true;
            continue;
        }

        if (argument.Equals("--doc", StringComparison.OrdinalIgnoreCase))
        {
            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException("--doc requires a document id, for example --doc A1.");
            }

            docId = args[++index];
            continue;
        }

        throw new ArgumentException($"Unknown fetch option '{argument}'.");
    }

    return new FetchOptions(force, docId);
}

static IngestOptions ParseIngestOptions(string[] args)
{
    var force = false;
    var chunking = ChunkingStrategy.SectionAware;
    DocumentVectorStrategyKind? docVector = null;

    for (var index = 0; index < args.Length; index++)
    {
        var argument = args[index];

        if (argument.Equals("--force", StringComparison.OrdinalIgnoreCase))
        {
            force = true;
            continue;
        }

        if (argument.Equals("--chunker", StringComparison.OrdinalIgnoreCase))
        {
            if (index + 1 >= args.Length)
            {
                throw new ArgumentException("--chunker requires SectionAware or FixedWindow.");
            }

            chunking = args[++index].ToLowerInvariant() switch
            {
                "sectionaware" or "section" => ChunkingStrategy.SectionAware,
                "fixedwindow" or "fixed" => ChunkingStrategy.FixedWindow,
                _ => throw new ArgumentException($"Unknown chunker '{args[index]}'.")
            };
            continue;
        }

        if (argument.Equals("--doc-vector", StringComparison.OrdinalIgnoreCase))
        {
            if (index + 1 >= args.Length)
            {
                throw new ArgumentException("--doc-vector requires Summary, Centroid, or Composite.");
            }

            docVector = args[++index].ToLowerInvariant() switch
            {
                "summary" => DocumentVectorStrategyKind.Summary,
                "centroid" => DocumentVectorStrategyKind.Centroid,
                "composite" => DocumentVectorStrategyKind.Composite,
                _ => throw new ArgumentException($"Unknown doc-vector strategy '{args[index]}'.")
            };
            continue;
        }

        throw new ArgumentException($"Unknown ingest option '{argument}'.");
    }

    return new IngestOptions(force, chunking, docVector);
}

static int PrintUsage()
{
    Console.WriteLine("Athena ingestion commands:");
    Console.WriteLine("  fetch [--force] [--doc <docId>]");
    Console.WriteLine("  ingest [--force] [--chunker SectionAware|FixedWindow] [--doc-vector Summary|Centroid|Composite]");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  dotnet run --project src/Athena.Ingestion -- fetch");
    Console.WriteLine("  dotnet run --project src/Athena.Ingestion -- ingest --force --doc-vector Centroid");
    return 1;
}

internal sealed record FetchOptions(bool Force, string? DocId);

internal sealed record IngestOptions(
    bool Force,
    ChunkingStrategy ChunkingStrategy,
    DocumentVectorStrategyKind? DocumentVectorStrategy);
