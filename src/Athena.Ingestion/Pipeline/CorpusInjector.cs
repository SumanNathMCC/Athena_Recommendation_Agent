using System.Text.Json;
using Athena.Core.Corpus;
using Athena.Core.Records;
using Athena.Ingestion.Extraction;
using Athena.Ingestion.Fetch;
using Athena.Ingestion.Injection;

namespace Athena.Ingestion.Pipeline;

public interface ICorpusInjector
{
    Task<CorpusPipelineOperationResult> InjectAsync(
        string repoRoot,
        bool force = false,
        CancellationToken ct = default);
}

public sealed class CorpusInjector : ICorpusInjector
{
    private const float OcrConfidenceThreshold = 0.85f;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly ICorpusManifestReader _manifestReader;

    public CorpusInjector(ICorpusManifestReader manifestReader)
    {
        _manifestReader = manifestReader;
    }

    public async Task<CorpusPipelineOperationResult> InjectAsync(
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
            var ingestedPath = CorpusPaths.GetIngestedPath(repoRoot, entry.DocId);

            var downloadStatus = CorpusStageInspector.GetDownloadStatus(pdfPath);
            var extractStatus = CorpusStageInspector.GetExtractStatus(extractedPath);

            if (extractStatus != PipelineStageStatus.Succeeded)
            {
                statuses.Add(new CorpusDocPipelineStatus(
                    entry.DocId,
                    entry.Title,
                    entry.LocalFile,
                    downloadStatus,
                    extractStatus,
                    PipelineStageStatus.NotStarted,
                    ExtractMessage: extractStatus == PipelineStageStatus.NotStarted
                        ? "No extraction artifact found. Run extract first."
                        : null));
                continue;
            }

            if (!force && File.Exists(ingestedPath))
            {
                statuses.Add(new CorpusDocPipelineStatus(
                    entry.DocId,
                    entry.Title,
                    entry.LocalFile,
                    downloadStatus,
                    extractStatus,
                    PipelineStageStatus.Skipped,
                    InjectMessage: "Injection artifact already exists."));
                continue;
            }

            try
            {
                var extracted = await ReadExtractedAsync(extractedPath, ct);
                var injected = BuildInjectedDocument(extracted);

                await WriteInjectedAsync(ingestedPath, injected, ct);

                statuses.Add(new CorpusDocPipelineStatus(
                    entry.DocId,
                    entry.Title,
                    entry.LocalFile,
                    downloadStatus,
                    extractStatus,
                    PipelineStageStatus.Succeeded,
                    InjectMessage: $"{injected.Chunks.Count} chunk(s) staged."));
            }
            catch (Exception ex)
            {
                statuses.Add(new CorpusDocPipelineStatus(
                    entry.DocId,
                    entry.Title,
                    entry.LocalFile,
                    downloadStatus,
                    extractStatus,
                    PipelineStageStatus.Failed,
                    InjectMessage: ex.Message));
            }
        }

        return CorpusStageInspector.ToOperationResult(statuses, stage => stage.Inject);
    }

    private static InjectedDocument BuildInjectedDocument(ExtractedDocument extracted)
    {
        var chunks = extracted.Pages
            .Where(page => !string.IsNullOrWhiteSpace(page.Text))
            .Select(page => new InjectedChunk
            {
                ChunkId = $"{extracted.DocId}-p{page.PageNumber:D4}",
                Text = page.Text,
                PageNumber = page.PageNumber,
                Kind = page.MeanConfidence < OcrConfidenceThreshold
                    ? ChunkKind.OcrProse
                    : ChunkKind.Prose
            })
            .ToList();

        return new InjectedDocument
        {
            DocId = extracted.DocId,
            Title = extracted.Title,
            InjectedAt = DateTimeOffset.UtcNow,
            Chunks = chunks
        };
    }

    private static async Task<ExtractedDocument> ReadExtractedAsync(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<ExtractedDocument>(stream, JsonOptions, ct)
            ?? throw new InvalidDataException($"Extraction artifact '{path}' is empty or invalid.");
    }

    private static async Task WriteInjectedAsync(
        string path,
        InjectedDocument document,
        CancellationToken ct)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, document, JsonOptions, ct);
    }
}
