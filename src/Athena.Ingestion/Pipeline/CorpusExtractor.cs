using Athena.Core.Corpus;
using Athena.Core.Records;
using Athena.Ingestion.Extraction;
using Athena.Ingestion.Fetch;
using Athena.Ingestion.VectorStore;

namespace Athena.Ingestion.Pipeline;

public interface ICorpusExtractor
{
    Task<CorpusPipelineOperationResult> ExtractAsync(
        string repoRoot,
        bool force = false,
        IReadOnlyCollection<string>? docIds = null,
        CancellationToken ct = default);
}

public sealed class CorpusExtractor : ICorpusExtractor
{
    private readonly ICorpusManifestReader _manifestReader;
    private readonly IDocumentMarkdownExtractor _markdownExtractor;
    private readonly ICorpusVectorIndexer _vectorIndexer;

    public CorpusExtractor(
        ICorpusManifestReader manifestReader,
        IDocumentMarkdownExtractor markdownExtractor,
        ICorpusVectorIndexer vectorIndexer)
    {
        _manifestReader = manifestReader;
        _markdownExtractor = markdownExtractor;
        _vectorIndexer = vectorIndexer;
    }

    public async Task<CorpusPipelineOperationResult> ExtractAsync(
        string repoRoot,
        bool force = false,
        IReadOnlyCollection<string>? docIds = null,
        CancellationToken ct = default)
    {
        var manifest = await _manifestReader.ReadAsync(repoRoot, ct);
        CorpusPaths.EnsureArtifactDirectories(repoRoot);

        var documents = DocumentIdFilter.Apply(
            CorpusDocumentCatalog.GetAllDocuments(manifest),
            entry => entry.DocId,
            docIds);
        var statuses = new List<CorpusDocPipelineStatus>();

        if (documents.Count == 0)
        {
            throw new InvalidOperationException("No documents were selected for extraction.");
        }

        foreach (var entry in documents)
        {
            ct.ThrowIfCancellationRequested();

            var pdfPath = CorpusPaths.GetPdfPath(repoRoot, manifest.DownloadDirectory, entry.LocalFile);
            var extractedPath = CorpusPaths.GetExtractedPath(repoRoot, entry.DocId);
            var ingestedPath = CorpusPaths.GetIngestedPath(repoRoot, entry.DocId);
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

            var (injectStatus, injectMessage) = await CorpusStageInspector.GetInjectStatusAsync(
                entry.DocId,
                ingestedPath,
                _vectorIndexer,
                ct);

            if (!force && File.Exists(extractedPath))
            {
                statuses.Add(new CorpusDocPipelineStatus(
                    entry.DocId,
                    entry.Title,
                    entry.LocalFile,
                    downloadStatus,
                    PipelineStageStatus.Skipped,
                    injectStatus,
                    ExtractMessage: "Extraction artifact already exists.",
                    InjectMessage: injectMessage));
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
                    injectStatus,
                    ExtractMessage:
                        $"{parsed.Sections.Count} section(s), {parsed.Blocks.Count} block(s) ({proseCount} prose, {tableCount} table).",
                    InjectMessage: injectMessage));
            }
            catch (Exception ex)
            {
                statuses.Add(new CorpusDocPipelineStatus(
                    entry.DocId,
                    entry.Title,
                    entry.LocalFile,
                    downloadStatus,
                    PipelineStageStatus.Failed,
                    injectStatus,
                    ExtractMessage: ex.Message,
                    InjectMessage: injectMessage));
            }
        }

        return CorpusStageInspector.ToOperationResult(statuses, stage => stage.Extract);
    }
}
