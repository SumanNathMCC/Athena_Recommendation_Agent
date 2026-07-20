using System.Net.Http.Headers;
using Athena.Ingestion.Fetch;

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

static int PrintUsage()
{
    Console.WriteLine("Athena ingestion commands:");
    Console.WriteLine("  fetch [--force] [--doc <docId>]");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  dotnet run --project src/Athena.Ingestion -- fetch");
    Console.WriteLine("  dotnet run --project src/Athena.Ingestion -- fetch --force");
    Console.WriteLine("  dotnet run --project src/Athena.Ingestion -- fetch --doc A1");
    return 1;
}

internal sealed record FetchOptions(bool Force, string? DocId);
