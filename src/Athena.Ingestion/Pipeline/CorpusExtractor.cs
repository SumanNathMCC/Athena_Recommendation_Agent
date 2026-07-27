using System.Text.Json;
using Athena.Core.Corpus;
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
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly ICorpusManifestReader _manifestReader;
    private readonly IPdfTextExtractor _textExtractor;

    public CorpusExtractor(ICorpusManifestReader manifestReader, IPdfTextExtractor textExtractor)
    {
        _manifestReader = manifestReader;
        _textExtractor = textExtractor;
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
                var pages = await _textExtractor.ExtractAsync(pdfPath, ct);

                var extracted = new ExtractedDocument
                {
                    DocId = entry.DocId,
                    Title = entry.Title,
                    SourcePdfPath = pdfPath,
                    ExtractedAt = DateTimeOffset.UtcNow,
                    Pages = pages
                        .Select(page => new ExtractedPage
                        {
                            PageNumber = page.PageNumber,
                            Text = page.Text,
                            MeanConfidence = page.MeanConfidence
                        })
                        .ToList()
                };

                await WriteExtractedAsync(extractedPath, extracted, ct);

                statuses.Add(new CorpusDocPipelineStatus(
                    entry.DocId,
                    entry.Title,
                    entry.LocalFile,
                    downloadStatus,
                    PipelineStageStatus.Succeeded,
                    CorpusStageInspector.GetInjectStatus(
                        CorpusPaths.GetIngestedPath(repoRoot, entry.DocId)),
                    ExtractMessage: $"{extracted.Pages.Count} page(s) extracted."));
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

    private static async Task WriteExtractedAsync(
        string path,
        ExtractedDocument document,
        CancellationToken ct)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, document, JsonOptions, ct);
    }
}
