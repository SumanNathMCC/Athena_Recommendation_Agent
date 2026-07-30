using Athena.Core.Corpus;

namespace Athena.Ingestion.Fetch;

public sealed class CorpusFetcher : ICorpusFetcher
{
    private readonly ICorpusManifestReader _manifestReader;
    private readonly HttpClient _httpClient;

    public CorpusFetcher(ICorpusManifestReader manifestReader, HttpClient httpClient)
    {
        _manifestReader = manifestReader;
        _httpClient = httpClient;
    }

    public async Task<FetchResult> FetchAsync(
        string repoRoot,
        bool force = false,
        IReadOnlyCollection<string>? docIds = null,
        CancellationToken ct = default)
    {
        var manifest = await _manifestReader.ReadAsync(repoRoot, ct);
        var downloadDirectory = GetDownloadDirectory(repoRoot, manifest.DownloadDirectory);
        Directory.CreateDirectory(downloadDirectory);

        var documents = Pipeline.DocumentIdFilter.Apply(
                manifest.Documents.Where(document => document.Fetch),
                document => document.DocId,
                docIds)
            .ToList();

        if (documents.Count == 0)
        {
            throw new InvalidOperationException(
                docIds is null
                    ? "No fetchable documents were found in the corpus manifest."
                    : "No matching fetchable documents were selected.");
        }

        var results = new List<FetchItemResult>();

        foreach (var document in documents)
        {
            ct.ThrowIfCancellationRequested();

            var targetPath = GetTargetPath(downloadDirectory, document.LocalFile);

            if (!force && IsExistingFileValid(targetPath))
            {
                Console.WriteLine($"SKIP  {document.DocId} -> {document.LocalFile}");
                results.Add(new FetchItemResult(
                    document.DocId,
                    document.LocalFile,
                    FetchItemStatus.Skipped));
                continue;
            }

            var temporaryPath = $"{targetPath}.{Guid.NewGuid():N}.download";

            try
            {
                Console.WriteLine($"GET   {document.DocId} <- {document.Url}");

                using var response = await _httpClient.GetAsync(
                    document.Url,
                    HttpCompletionOption.ResponseHeadersRead,
                    ct);
                response.EnsureSuccessStatusCode();

                await using (var source = await response.Content.ReadAsStreamAsync(ct))
                await using (var destination = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    useAsync: true))
                {
                    await source.CopyToAsync(destination, ct);
                }

                if (!IsPdf(temporaryPath))
                {
                    throw new InvalidDataException(
                        "The downloaded response does not contain a valid PDF header.");
                }

                File.Move(temporaryPath, targetPath, overwrite: true);

                Console.WriteLine($"OK    {document.DocId} -> {targetPath}");
                results.Add(new FetchItemResult(
                    document.DocId,
                    document.LocalFile,
                    FetchItemStatus.Downloaded));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                DeleteIfExists(temporaryPath);
                throw;
            }
            catch (Exception ex)
            {
                DeleteIfExists(temporaryPath);

                var message = $"{document.DocId} ({document.Url}): {ex.Message}";
                Console.Error.WriteLine($"FAIL  {message}");
                results.Add(new FetchItemResult(
                    document.DocId,
                    document.LocalFile,
                    FetchItemStatus.Failed,
                    message));
            }
        }

        return new FetchResult(
            results.Count(result => result.Status == FetchItemStatus.Downloaded),
            results.Count(result => result.Status == FetchItemStatus.Skipped),
            results.Count(result => result.Status == FetchItemStatus.Failed),
            results);
    }

    private static string GetDownloadDirectory(string repoRoot, string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new InvalidDataException(
                "The corpus manifest must define downloadDirectory.");
        }

        var rootPath = Path.GetFullPath(repoRoot);
        var downloadPath = Path.GetFullPath(
            Path.Combine(rootPath, configuredPath.Replace('/', Path.DirectorySeparatorChar)));

        if (!downloadPath.StartsWith(
                rootPath + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "downloadDirectory must resolve inside the repository root.");
        }

        return downloadPath;
    }

    private static string GetTargetPath(string downloadDirectory, string localFile)
    {
        if (string.IsNullOrWhiteSpace(localFile) ||
            !Path.GetFileName(localFile).Equals(localFile, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Invalid localFile value '{localFile}' in corpus manifest.");
        }

        return Path.Combine(downloadDirectory, localFile);
    }

    private static bool IsExistingFileValid(string path) =>
        File.Exists(path) && new FileInfo(path).Length > 5 && IsPdf(path);

    private static bool IsPdf(string path)
    {
        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[5];
        return stream.Read(header) == header.Length && header.SequenceEqual("%PDF-"u8);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
