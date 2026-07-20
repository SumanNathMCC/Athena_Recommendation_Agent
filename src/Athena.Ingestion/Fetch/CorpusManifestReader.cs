using System.Text.Json;
using Athena.Core.Corpus;

namespace Athena.Ingestion.Fetch;

public sealed class CorpusManifestReader : ICorpusManifestReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public async Task<CorpusManifest> ReadAsync(
        string repoRoot,
        CancellationToken ct = default)
    {
        var manifestPath = Path.Combine(repoRoot, "corpus", "manifest.json");

        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException("Corpus manifest was not found.", manifestPath);
        }

        await using var stream = File.OpenRead(manifestPath);
        var manifest = await JsonSerializer.DeserializeAsync<CorpusManifest>(
            stream,
            JsonOptions,
            ct);

        return manifest
            ?? throw new InvalidOperationException("corpus/manifest.json is empty or invalid.");
    }
}
