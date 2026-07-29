using Athena.Core.Corpus;
using Athena.Core.Records;
using Athena.Ingestion.Extraction;
using Athena.Ingestion.Fetch;

namespace Athena.Ingestion.Pipeline;

public interface ICorpusExtractor
{
    Task<CorpusPipelineOperationResult> ExtractAsync(
        string repoRoot,
        bool force = false,
        CancellationToken ct = default);
}

public sealed class CorpusExtractor : ICorpusExtractor
{
    private readonly ICorpusManifestReader _manifestReader;
    private readonly IDocumentMarkdownExtractor _markdownExtractor;

    public CorpusExtractor(
        ICorpusManifestReader manifestReader,
        IDocumentMarkdownExtractor markdownExtractor)
    {
        _manifestReader = manifestReader;
        _markdownExtractor = markdownExtractor;
    }

    public async Task<CorpusPipelineOperationResult> ExtractAsync(
        string repoRoot,
        bool force = false,
        CancellationToken ct = default)
    {
        var manifest = await _manifestReader.ReadAsync(repoRoot, ct);
        CorpusPaths.EnsureArtifactDirectories(repoRoot);

        var documents = CorpusDocumentCatalog.GetAllDocuments(manifest);
        var statuses = new List<CorpusDocPipelineStatus>();

        foreach (var entry in documents)
        {
            ct.ThrowIfCancellationRequested();

            var pdfPath = CorpusPaths.GetPdfPath(repoRoot, manifest.DownloadDirectory, entry.LocalFile);
            var extractedPath = CorpusPaths.GetExtractedPath(repoRoot, entry.DocId);
            var downloadStatus = CorpusStageInspector.GetDownloadStatus(pdfPath);

            if (downloadStatus != PipelineStageStatus.Succeeded)
            {
                statuses.Add(new CorpusDocPipelineStatus(
                    entry.DocId,
                    entry.Title,
                    entry.LocalFile,
                    downloadStatus,
                    PipelineStageStatus.NotStarted,
                    PipelineStageStatus.NotStarted,
                    DownloadMessage: "PDF is not available locally. Run fetch first."));
                continue;
            }

            if (!force && File.Exists(extractedPath))
            {
                statuses.Add(new CorpusDocPipelineStatus(
                    entry.DocId,
                    entry.Title,
                    entry.LocalFile,
                    downloadStatus,
                    PipelineStageStatus.Skipped,
                    CorpusStageInspector.GetInjectStatus(
                        CorpusPaths.GetIngestedPath(repoRoot, entry.DocId)),
                    ExtractMessage: "Extraction artifact already exists."));
                continue;
            }

            try
            {
                var markdown = await _markdownExtractor.ExtractToMarkdownAsync(
                    pdfPath,
                    new DocumentExtractionContext
                    {
                        DocId = entry.DocId,
                        Title = entry.Title,
                        SourcePdfPath = pdfPath
                    },
                    ct);

                await File.WriteAllTextAsync(extractedPath, markdown, ct);

                var parsed = ExtractedMarkdownParser.Parse(markdown);
                var tableCount = parsed.Blocks.Count(block => block.Kind == ChunkKind.Table);
                var proseCount = parsed.Blocks.Count - tableCount;

                statuses.Add(new CorpusDocPipelineStatus(
                    entry.DocId,
                    entry.Title,
                    entry.LocalFile,
                    downloadStatus,
                    PipelineStageStatus.Succeeded,
                    CorpusStageInspector.GetInjectStatus(
                        CorpusPaths.GetIngestedPath(repoRoot, entry.DocId)),
                    ExtractMessage:
                        $"{parsed.Sections.Count} section(s), {parsed.Blocks.Count} block(s) ({proseCount} prose, {tableCount} table)."));
            }
            catch (Exception ex)
            {
                statuses.Add(new CorpusDocPipelineStatus(
                    entry.DocId,
                    entry.Title,
                    entry.LocalFile,
                    downloadStatus,
                    PipelineStageStatus.Failed,
                    CorpusStageInspector.GetInjectStatus(
                        CorpusPaths.GetIngestedPath(repoRoot, entry.DocId)),
                    ExtractMessage: ex.Message));
            }
        }

        return CorpusStageInspector.ToOperationResult(statuses, stage => stage.Extract);
    }
}
