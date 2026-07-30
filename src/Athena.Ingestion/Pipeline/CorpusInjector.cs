using Athena.Core.Corpus;
using Athena.Core.Records;
using Athena.Ingestion.Chunking;
using Athena.Ingestion.DocVectors;
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
        DocumentVectorStrategyKind? documentVectorStrategy = null,
        IReadOnlyCollection<string>? docIds = null,
        CancellationToken ct = default);
}

public sealed class CorpusInjector : ICorpusInjector
{
    private readonly ICorpusManifestReader _manifestReader;
    private readonly IChunkerFactory _chunkerFactory;
    private readonly IDocumentSummariser _summariser;
    private readonly ICorpusEmbeddingService _embeddingService;
    private readonly IDocumentVectorStrategyFactory _docVectorFactory;
    private readonly ICorpusVectorIndexer _vectorIndexer;
    private readonly AzureFoundryOptions _azureOptions;

    public CorpusInjector(
        ICorpusManifestReader manifestReader,
        IChunkerFactory chunkerFactory,
        IDocumentSummariser summariser,
        ICorpusEmbeddingService embeddingService,
        IDocumentVectorStrategyFactory docVectorFactory,
        ICorpusVectorIndexer vectorIndexer,
        IOptions<AzureFoundryOptions> azureOptions)
    {
        _manifestReader = manifestReader;
        _chunkerFactory = chunkerFactory;
        _summariser = summariser;
        _embeddingService = embeddingService;
        _docVectorFactory = docVectorFactory;
        _vectorIndexer = vectorIndexer;
        _azureOptions = azureOptions.Value;
    }

    public async Task<CorpusPipelineOperationResult> InjectAsync(
        string repoRoot,
        bool force = false,
        ChunkingStrategy strategy = ChunkingStrategy.SectionAware,
        DocumentVectorStrategyKind? documentVectorStrategy = null,
        IReadOnlyCollection<string>? docIds = null,
        CancellationToken ct = default)
    {
        EnsureAzureFoundryConfigured();

        var manifest = await _manifestReader.ReadAsync(repoRoot, ct);
        CorpusPaths.EnsureArtifactDirectories(repoRoot);

        var documents = DocumentIdFilter.Apply(
            CorpusDocumentCatalog.GetAllDocuments(manifest),
            entry => entry.DocId,
            docIds);
        if (documents.Count == 0)
        {
            throw new InvalidOperationException("No documents were selected for injection.");
        }

        var manifestById = manifest.Documents.ToDictionary(document => document.DocId, StringComparer.OrdinalIgnoreCase);
        var chunker = _chunkerFactory.GetChunker(strategy);
        var docVector = documentVectorStrategy is null
            ? _docVectorFactory.GetDefaultStrategy()
            : _docVectorFactory.GetStrategy(documentVectorStrategy.Value);
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
                        docVector,
                        ct);

                    statuses.Add(new CorpusDocPipelineStatus(
                        entry.DocId,
                        entry.Title,
                        entry.LocalFile,
                        downloadStatus,
                        extractStatus,
                        PipelineStageStatus.Succeeded,
                        InjectMessage:
                            $"Reloaded {reloaded.ChunkCount} chunk vector(s) into SK in-memory store ({docVector.Name})."));
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

                var chunkEmbeddings = await _embeddingService.EmbedBatchAsync(
                    chunkDrafts.Select(chunk => chunk.Text).ToList(),
                    ct);

                var chunkRecords = CorpusVectorIndexer.BuildChunkRecords(
                    metadata,
                    chunkDrafts,
                    chunkIds,
                    chunkEmbeddings);

                var docRecord = CorpusVectorIndexer.BuildDocRecord(metadata, summary);
                docRecord.Embedding = await docVector.BuildAsync(docRecord, chunkRecords, ct);

                var chunkedMarkdown = ChunkedMarkdownWriter.Write(metadata, chunker.Name, summary, chunkDrafts);
                await File.WriteAllTextAsync(ingestedPath, chunkedMarkdown, ct);
                await _vectorIndexer.UpsertRecordsAsync(chunkRecords, docRecord, replaceExisting: force, ct);

                statuses.Add(new CorpusDocPipelineStatus(
                    entry.DocId,
                    entry.Title,
                    entry.LocalFile,
                    downloadStatus,
                    extractStatus,
                    PipelineStageStatus.Succeeded,
                    InjectMessage:
                        $"{chunkDrafts.Count} chunk(s) via {chunker.Name}; doc vector={docVector.Name}."));
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
        IDocumentVectorStrategy docVector,
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
        var chunkEmbeddings = await _embeddingService.EmbedBatchAsync(
            chunkDrafts.Select(chunk => chunk.Text).ToList(),
            ct);

        var chunkRecords = CorpusVectorIndexer.BuildChunkRecords(
            metadata,
            chunkDrafts,
            chunkIds,
            chunkEmbeddings);

        var docRecord = CorpusVectorIndexer.BuildDocRecord(metadata, summary);
        docRecord.Embedding = await docVector.BuildAsync(docRecord, chunkRecords, ct);

        await _vectorIndexer.UpsertRecordsAsync(chunkRecords, docRecord, replaceExisting: true, ct);

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
