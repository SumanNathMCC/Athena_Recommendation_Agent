using System.Diagnostics;
using Athena.Core.Corpus;
using Athena.Core.Records;
using Athena.Ingestion.Chunking;
using Athena.Ingestion.DocVectors;
using Athena.Ingestion.Embeddings;
using Athena.Ingestion.Extraction;
using Athena.Ingestion.Fetch;
using Athena.Ingestion.Pipeline;
using Athena.Ingestion.Summarization;
using Athena.Ingestion.VectorStore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Athena.Ingestion;

/// <summary>
/// Assignment-shaped ingestion orchestrator over the Athena extract → chunk → summarise → embed flow.
/// Uses Azure Document Intelligence artifacts (not PdfPig/OCR).
/// </summary>
public sealed class IngestionPipeline
{
    private readonly ICorpusManifestReader _manifestReader;
    private readonly IChunkerFactory _chunkerFactory;
    private readonly IDocumentSummariser _summariser;
    private readonly ICorpusEmbeddingService _embeddingService;
    private readonly IDocumentVectorStrategyFactory _docVectorFactory;
    private readonly ICorpusVectorIndexer _vectorIndexer;
    private readonly AzureFoundryOptions _azureOptions;
    private readonly ILogger<IngestionPipeline> _logger;

    public IngestionPipeline(
        ICorpusManifestReader manifestReader,
        IChunkerFactory chunkerFactory,
        IDocumentSummariser summariser,
        ICorpusEmbeddingService embeddingService,
        IDocumentVectorStrategyFactory docVectorFactory,
        ICorpusVectorIndexer vectorIndexer,
        IOptions<AzureFoundryOptions> azureOptions,
        ILogger<IngestionPipeline> logger)
    {
        _manifestReader = manifestReader;
        _chunkerFactory = chunkerFactory;
        _summariser = summariser;
        _embeddingService = embeddingService;
        _docVectorFactory = docVectorFactory;
        _vectorIndexer = vectorIndexer;
        _azureOptions = azureOptions.Value;
        _logger = logger;
    }

    public Task<IngestionReport> RunAsync(
        string repoRoot,
        CancellationToken ct = default) =>
        RunAsync(
            repoRoot,
            force: false,
            ChunkingStrategy.SectionAware,
            documentVectorStrategy: null,
            ct);

    public async Task<IngestionReport> RunAsync(
        string repoRoot,
        bool force,
        ChunkingStrategy chunkingStrategy,
        DocumentVectorStrategyKind? documentVectorStrategy,
        CancellationToken ct = default)
    {
        if (!_azureOptions.IsConfigured)
        {
            throw new InvalidOperationException(
                "AzureFoundry is not configured. Set Endpoint, ApiKey, ChatDeployment, and EmbeddingDeployment.");
        }

        var stopwatch = Stopwatch.StartNew();
        var warnings = new List<string>();
        var documentsProcessed = 0;
        var chunksWritten = 0;
        var tablesExtracted = 0;

        var manifest = await _manifestReader.ReadAsync(repoRoot, ct);
        CorpusPaths.EnsureArtifactDirectories(repoRoot);

        var chunker = _chunkerFactory.GetChunker(chunkingStrategy);
        var docVectorStrategy = documentVectorStrategy is null
            ? _docVectorFactory.GetDefaultStrategy()
            : _docVectorFactory.GetStrategy(documentVectorStrategy.Value);

        var documents = CorpusDocumentCatalog.GetAllDocuments(manifest);
        var manifestById = manifest.Documents.ToDictionary(d => d.DocId, StringComparer.OrdinalIgnoreCase);

        foreach (var entry in documents)
        {
            ct.ThrowIfCancellationRequested();

            var extractedPath = CorpusPaths.GetExtractedPath(repoRoot, entry.DocId);
            var ingestedPath = CorpusPaths.GetIngestedPath(repoRoot, entry.DocId);
            var pdfPath = CorpusPaths.GetPdfPath(repoRoot, manifest.DownloadDirectory, entry.LocalFile);

            if (!File.Exists(extractedPath))
            {
                warnings.Add($"{entry.DocId}: missing extraction artifact; run extract first.");
                continue;
            }

            if (!force && File.Exists(ingestedPath) && await _vectorIndexer.IsDocumentIndexedAsync(entry.DocId, ct))
            {
                warnings.Add($"{entry.DocId}: skipped (already ingested).");
                continue;
            }

            try
            {
                if (!manifestById.TryGetValue(entry.DocId, out var manifestDoc))
                {
                    warnings.Add($"{entry.DocId}: missing from manifest.");
                    continue;
                }

                var metadata = DocumentMetadata.FromManifest(manifestDoc, pdfPath);
                var markdown = await File.ReadAllTextAsync(extractedPath, ct);
                var extracted = ExtractedMarkdownParser.Parse(markdown);
                tablesExtracted += extracted.Blocks.Count(block => block.Kind == ChunkKind.Table);

                var documentText = ExtractedDocumentTextBuilder.BuildPlainText(extracted);
                var summary = await _summariser.SummarizeAsync(metadata, documentText, ct);
                var chunkDrafts = chunker.Chunk(metadata, extracted);

                if (chunkDrafts.Count == 0)
                {
                    warnings.Add($"{entry.DocId}: chunker produced no chunks.");
                    continue;
                }

                var chunkIds = chunkDrafts
                    .Select((chunk, index) => ChunkedMarkdownWriter.BuildChunkId(metadata.DocId, chunk, index))
                    .ToList();
                var chunkEmbeddings = await _embeddingService.EmbedBatchAsync(
                    chunkDrafts.Select(chunk => chunk.Text).ToList(),
                    ct);

                var chunkRecords = CorpusVectorIndexer.BuildChunkRecords(
                    metadata,
                    chunkDrafts,
                    chunkIds,
                    chunkEmbeddings);

                var docRecord = CorpusVectorIndexer.BuildDocRecord(metadata, summary);
                docRecord.Embedding = await docVectorStrategy.BuildAsync(docRecord, chunkRecords, ct);

                var chunkedMarkdown = ChunkedMarkdownWriter.Write(metadata, chunker.Name, summary, chunkDrafts);
                await File.WriteAllTextAsync(ingestedPath, chunkedMarkdown, ct);
                await _vectorIndexer.UpsertRecordsAsync(chunkRecords, docRecord, replaceExisting: force, ct);

                documentsProcessed++;
                chunksWritten += chunkRecords.Count;

                _logger.LogInformation(
                    "Ingested {DocId}: {ChunkCount} chunks via {Chunker}/{DocVector}.",
                    entry.DocId,
                    chunkRecords.Count,
                    chunker.Name,
                    docVectorStrategy.Name);
            }
            catch (Exception ex)
            {
                warnings.Add($"{entry.DocId}: {ex.Message}");
                _logger.LogError(ex, "Failed to ingest {DocId}.", entry.DocId);
            }
        }

        stopwatch.Stop();
        return new IngestionReport(
            DocumentsProcessed: documentsProcessed,
            ChunksWritten: chunksWritten,
            PagesOcrd: 0,
            TablesExtracted: tablesExtracted,
            Warnings: warnings,
            Elapsed: stopwatch.Elapsed,
            ChunkerName: chunker.Name,
            DocumentVectorStrategy: docVectorStrategy.Name);
    }
}
