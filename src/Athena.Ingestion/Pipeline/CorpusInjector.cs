using Athena.Core.Corpus;
using Athena.Core.Records;
using Athena.Ingestion.Chunking;
using Athena.Ingestion.Embeddings;
using Athena.Ingestion.Extraction;
using Athena.Ingestion.Fetch;
using Athena.Ingestion.Summarization;
using Athena.Ingestion.VectorStore;
using Microsoft.Extensions.Options;

namespace Athena.Ingestion.Pipeline;

public interface ICorpusInjector
{
    Task<CorpusPipelineOperationResult> InjectAsync(
        string repoRoot,
        bool force = false,
        ChunkingStrategy strategy = ChunkingStrategy.SectionAware,
        CancellationToken ct = default);
}

public sealed class CorpusInjector : ICorpusInjector
{
    private readonly ICorpusManifestReader _manifestReader;
    private readonly IChunkerFactory _chunkerFactory;
    private readonly IDocumentSummariser _summariser;
    private readonly ICorpusEmbeddingService _embeddingService;
    private readonly ICorpusVectorIndexer _vectorIndexer;
    private readonly AzureFoundryOptions _azureOptions;

    public CorpusInjector(
        ICorpusManifestReader manifestReader,
        IChunkerFactory chunkerFactory,
        IDocumentSummariser summariser,
        ICorpusEmbeddingService embeddingService,
        ICorpusVectorIndexer vectorIndexer,
        IOptions<AzureFoundryOptions> azureOptions)
    {
        _manifestReader = manifestReader;
        _chunkerFactory = chunkerFactory;
        _summariser = summariser;
        _embeddingService = embeddingService;
        _vectorIndexer = vectorIndexer;
        _azureOptions = azureOptions.Value;
    }

    public async Task<CorpusPipelineOperationResult> InjectAsync(
        string repoRoot,
        bool force = false,
        ChunkingStrategy strategy = ChunkingStrategy.SectionAware,
        CancellationToken ct = default)
    {
        EnsureAzureFoundryConfigured();

        var manifest = await _manifestReader.ReadAsync(repoRoot, ct);
        CorpusPaths.EnsureArtifactDirectories(repoRoot);

        var documents = CorpusDocumentCatalog.GetAllDocuments(manifest);
        var manifestById = manifest.Documents.ToDictionary(document => document.DocId, StringComparer.OrdinalIgnoreCase);
        var chunker = _chunkerFactory.GetChunker(strategy);
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
                if (await _vectorIndexer.IsDocumentIndexedAsync(entry.DocId, ct))
                {
                    statuses.Add(new CorpusDocPipelineStatus(
                        entry.DocId,
                        entry.Title,
                        entry.LocalFile,
                        downloadStatus,
                        extractStatus,
                        PipelineStageStatus.Skipped,
                        InjectMessage: "Chunked markdown and in-memory vectors already exist."));
                    continue;
                }

                try
                {
                    var reloaded = await ReloadVectorsFromIngestedMarkdownAsync(
                        ingestedPath,
                        manifestById,
                        pdfPath,
                        entry.DocId,
                        ct);

                    statuses.Add(new CorpusDocPipelineStatus(
                        entry.DocId,
                        entry.Title,
                        entry.LocalFile,
                        downloadStatus,
                        extractStatus,
                        PipelineStageStatus.Succeeded,
                        InjectMessage:
                            $"Reloaded {reloaded.ChunkCount} chunk vector(s) into Semantic Kernel in-memory store."));
                    continue;
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
                    continue;
                }
            }

            try
            {
                var markdown = await File.ReadAllTextAsync(extractedPath, ct);
                var extracted = ExtractedMarkdownParser.Parse(markdown);

                if (!manifestById.TryGetValue(entry.DocId, out var manifestDoc))
                {
                    throw new InvalidOperationException($"Document '{entry.DocId}' was not found in manifest.json.");
                }

                var metadata = DocumentMetadata.FromManifest(manifestDoc, pdfPath);
                var documentText = ExtractedDocumentTextBuilder.BuildPlainText(extracted);
                var summary = await _summariser.SummarizeAsync(metadata, documentText, ct);
                var chunkDrafts = chunker.Chunk(metadata, extracted);

                if (chunkDrafts.Count == 0)
                {
                    throw new InvalidOperationException("Chunker produced no chunks for this document.");
                }

                var chunkIds = chunkDrafts
                    .Select((chunk, index) => ChunkedMarkdownWriter.BuildChunkId(metadata.DocId, chunk, index))
                    .ToList();

                var chunkTexts = chunkDrafts.Select(chunk => chunk.Text).ToList();
                var chunkEmbeddings = await _embeddingService.EmbedBatchAsync(chunkTexts, ct);
                var docEmbedding = await _embeddingService.EmbedAsync(summary.Summary, ct);

                var chunkedMarkdown = ChunkedMarkdownWriter.Write(metadata, chunker.Name, summary, chunkDrafts);

                await File.WriteAllTextAsync(ingestedPath, chunkedMarkdown, ct);
                await _vectorIndexer.UpsertDocumentAsync(
                    metadata,
                    summary,
                    chunkDrafts,
                    chunkIds,
                    chunkEmbeddings,
                    docEmbedding,
                    replaceExisting: force,
                    ct);

                statuses.Add(new CorpusDocPipelineStatus(
                    entry.DocId,
                    entry.Title,
                    entry.LocalFile,
                    downloadStatus,
                    extractStatus,
                    PipelineStageStatus.Succeeded,
                    InjectMessage:
                        $"{chunkDrafts.Count} chunk(s) via {chunker.Name}; summary + embeddings stored in SK in-memory vector store."));
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

    private async Task<(int ChunkCount, string DocId)> ReloadVectorsFromIngestedMarkdownAsync(
        string ingestedPath,
        IReadOnlyDictionary<string, CorpusDocument> manifestById,
        string pdfPath,
        string docId,
        CancellationToken ct)
    {
        var markdown = await File.ReadAllTextAsync(ingestedPath, ct);
        var parsed = ChunkedMarkdownParser.Parse(markdown);

        if (parsed.Chunks.Count == 0)
        {
            throw new InvalidOperationException("Ingested markdown contains no chunks.");
        }

        if (!manifestById.TryGetValue(docId, out var manifestDoc))
        {
            throw new InvalidOperationException($"Document '{docId}' was not found in manifest.json.");
        }

        var metadata = DocumentMetadata.FromManifest(manifestDoc, pdfPath);
        var summary = new DocumentSummaryResult
        {
            Summary = parsed.Summary,
            Topics = parsed.Topics.ToList()
        };
        var chunkDrafts = parsed.Chunks
            .Select(chunk => new ChunkDraft
            {
                Text = chunk.Text,
                PageNumber = chunk.PageNumber,
                Section = chunk.Section,
                Kind = chunk.Kind
            })
            .ToList();
        var chunkIds = parsed.Chunks.Select(chunk => chunk.ChunkId).ToList();
        var chunkTexts = chunkDrafts.Select(chunk => chunk.Text).ToList();
        var chunkEmbeddings = await _embeddingService.EmbedBatchAsync(chunkTexts, ct);
        var docEmbedding = await _embeddingService.EmbedAsync(summary.Summary, ct);

        await _vectorIndexer.UpsertDocumentAsync(
            metadata,
            summary,
            chunkDrafts,
            chunkIds,
            chunkEmbeddings,
            docEmbedding,
            replaceExisting: true,
            ct);

        return (chunkDrafts.Count, docId);
    }

    private void EnsureAzureFoundryConfigured()
    {
        if (!_azureOptions.IsConfigured)
        {
            throw new InvalidOperationException(
                "AzureFoundry is not configured. Set Endpoint, ApiKey, ChatDeployment, and EmbeddingDeployment " +
                "for inject (LLM summary + embeddings via Semantic Kernel).");
        }
    }
}
