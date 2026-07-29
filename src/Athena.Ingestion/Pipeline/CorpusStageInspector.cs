using Athena.Core.Corpus;

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
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var statuses = new List<CorpusDocPipelineStatus>();

        foreach (var entry in CorpusDocumentCatalog.GetAllDocuments(manifest))
        {
            var pdfPath = CorpusPaths.GetPdfPath(repoRoot, manifest.DownloadDirectory, entry.LocalFile);
            var extractedPath = CorpusPaths.GetExtractedPath(repoRoot, entry.DocId);
            var ingestedPath = CorpusPaths.GetIngestedPath(repoRoot, entry.DocId);

            statuses.Add(new CorpusDocPipelineStatus(
                entry.DocId,
                entry.Title,
                entry.LocalFile,
                GetDownloadStatus(pdfPath),
                GetExtractStatus(extractedPath),
                GetInjectStatus(ingestedPath)));
        }

        return await Task.FromResult(statuses);
    }

    public static PipelineStageStatus GetDownloadStatus(string pdfPath) =>
        IsValidPdf(pdfPath) ? PipelineStageStatus.Succeeded : PipelineStageStatus.NotStarted;

    public static PipelineStageStatus GetExtractStatus(string extractedPath) =>
        File.Exists(extractedPath) ? PipelineStageStatus.Succeeded : PipelineStageStatus.NotStarted;

    public static PipelineStageStatus GetInjectStatus(string ingestedPath) =>
        File.Exists(ingestedPath) ? PipelineStageStatus.Succeeded : PipelineStageStatus.NotStarted;

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
