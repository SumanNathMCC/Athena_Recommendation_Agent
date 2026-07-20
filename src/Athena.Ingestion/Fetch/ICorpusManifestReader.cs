using Athena.Core.Corpus;

namespace Athena.Ingestion.Fetch;

public interface ICorpusManifestReader
{
    Task<CorpusManifest> ReadAsync(string repoRoot, CancellationToken ct = default);
}
