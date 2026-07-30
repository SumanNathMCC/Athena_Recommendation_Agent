using Athena.Core.Corpus;
using Athena.Ingestion.VectorStore;

namespace Athena.Ingestion.Pipeline;

internal static class CorpusDocumentCatalog
{
    public static IReadOnlyList<CorpusDocumentEntry> GetAllDocuments(CorpusManifest manifest)
    {
        return manifest.Documents
            .Where(document => document.Fetch)
            .Select(document => new CorpusDocumentEntry(
                document.DocId,
                document.Title,
                document.LocalFile))
            .OrderBy(entry => entry.DocId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

internal sealed record CorpusDocumentEntry(string DocId, string Title, string LocalFile);

internal static class CorpusStageInspector
{
    public static async Task<IReadOnlyList<CorpusDocPipelineStatus>> InspectAsync(
        CorpusManifest manifest,
        string repoRoot,
        ICorpusVectorIndexer vectorIndexer,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(vectorIndexer);
        ct.ThrowIfCancellationRequested();

        var statuses = new List<CorpusDocPipelineStatus>();

        foreach (var entry in CorpusDocumentCatalog.GetAllDocuments(manifest))
        {
            var pdfPath = CorpusPaths.GetPdfPath(repoRoot, manifest.DownloadDirectory, entry.LocalFile);
            var extractedPath = CorpusPaths.GetExtractedPath(repoRoot, entry.DocId);
            var ingestedPath = CorpusPaths.GetIngestedPath(repoRoot, entry.DocId);
            var (injectStatus, injectMessage) = await GetInjectStatusAsync(
                entry.DocId,
                ingestedPath,
                vectorIndexer,
                ct).ConfigureAwait(false);

            statuses.Add(new CorpusDocPipelineStatus(
                entry.DocId,
                entry.Title,
                entry.LocalFile,
                GetDownloadStatus(pdfPath),
                GetExtractStatus(extractedPath),
                injectStatus,
                InjectMessage: injectMessage));
        }

        return statuses;
    }

    public static PipelineStageStatus GetDownloadStatus(string pdfPath) =>
        IsValidPdf(pdfPath) ? PipelineStageStatus.Succeeded : PipelineStageStatus.NotStarted;

    public static PipelineStageStatus GetExtractStatus(string extractedPath) =>
        File.Exists(extractedPath) ? PipelineStageStatus.Succeeded : PipelineStageStatus.NotStarted;

    public static async Task<(PipelineStageStatus Status, string? Message)> GetInjectStatusAsync(
        string docId,
        string ingestedPath,
        ICorpusVectorIndexer vectorIndexer,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(vectorIndexer);

        var hasMarkdown = File.Exists(ingestedPath);
        var hasVectors = await vectorIndexer.IsDocumentIndexedAsync(docId, ct).ConfigureAwait(false);

        if (hasMarkdown && hasVectors)
        {
            return (PipelineStageStatus.Succeeded, null);
        }

        if (hasMarkdown && !hasVectors)
        {
            return (
                PipelineStageStatus.NotStarted,
                "Ingested markdown is on disk, but vectors are not in memory. Run Inject to reload.");
        }

        return (PipelineStageStatus.NotStarted, null);
    }

    public static CorpusPipelineOperationResult ToOperationResult(
        IReadOnlyList<CorpusDocPipelineStatus> documents,
        Func<CorpusDocPipelineStatus, PipelineStageStatus> stageSelector) =>
        new(
            documents.Count(document => stageSelector(document) == PipelineStageStatus.Succeeded),
            documents.Count(document => stageSelector(document) == PipelineStageStatus.Skipped),
            documents.Count(document => stageSelector(document) == PipelineStageStatus.Failed),
            documents);

    private static bool IsValidPdf(string path) =>
        File.Exists(path) && new FileInfo(path).Length > 5 && HasPdfHeader(path);

    private static bool HasPdfHeader(string path)
    {
        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[5];
        return stream.Read(header) == header.Length && header.SequenceEqual("%PDF-"u8);
    }
}
