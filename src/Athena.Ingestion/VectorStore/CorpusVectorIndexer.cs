using Athena.Core.Records;
using Athena.Ingestion.Chunking;
using Athena.Ingestion.Summarization;
using Microsoft.Extensions.VectorData;

namespace Athena.Ingestion.VectorStore;

public interface ICorpusVectorIndexer
{
    Task<bool> IsDocumentIndexedAsync(string docId, CancellationToken ct = default);

    Task UpsertRecordsAsync(
        IReadOnlyList<ChunkRecord> chunkRecords,
        DocRecord docRecord,
        bool replaceExisting,
        CancellationToken ct = default);

    Task UpsertDocumentAsync(
        DocumentMetadata metadata,
        DocumentSummaryResult summary,
        IReadOnlyList<ChunkDraft> chunks,
        IReadOnlyList<string> chunkIds,
        IReadOnlyList<ReadOnlyMemory<float>> chunkEmbeddings,
        ReadOnlyMemory<float> docEmbedding,
        bool replaceExisting,
        CancellationToken ct = default);
}

public sealed class CorpusVectorIndexer : ICorpusVectorIndexer
{
    private readonly CorpusVectorStore _vectorStore;

    public CorpusVectorIndexer(CorpusVectorStore vectorStore)
    {
        _vectorStore = vectorStore;
    }

    public async Task<bool> IsDocumentIndexedAsync(string docId, CancellationToken ct = default)
    {
        var docCollection = await _vectorStore.GetDocumentCollectionAsync(ct).ConfigureAwait(false);
        return await docCollection.GetAsync(docId, cancellationToken: ct).ConfigureAwait(false) is not null;
    }

    public async Task UpsertRecordsAsync(
        IReadOnlyList<ChunkRecord> chunkRecords,
        DocRecord docRecord,
        bool replaceExisting,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(chunkRecords);
        ArgumentNullException.ThrowIfNull(docRecord);

        var chunkCollection = await _vectorStore.GetChunkCollectionAsync(ct);
        var docCollection = await _vectorStore.GetDocumentCollectionAsync(ct);

        if (replaceExisting)
        {
            await RemoveExistingDocumentAsync(docRecord.DocId, chunkCollection, docCollection, ct);
        }

        if (chunkRecords.Count > 0)
        {
            await chunkCollection.UpsertAsync(chunkRecords, ct);
        }

        await docCollection.UpsertAsync(docRecord, ct);
    }

    public async Task UpsertDocumentAsync(
        DocumentMetadata metadata,
        DocumentSummaryResult summary,
        IReadOnlyList<ChunkDraft> chunks,
        IReadOnlyList<string> chunkIds,
        IReadOnlyList<ReadOnlyMemory<float>> chunkEmbeddings,
        ReadOnlyMemory<float> docEmbedding,
        bool replaceExisting,
        CancellationToken ct = default)
    {
        if (chunks.Count != chunkIds.Count || chunks.Count != chunkEmbeddings.Count)
        {
            throw new InvalidOperationException("Chunk, id, and embedding counts must match.");
        }

        var chunkRecords = BuildChunkRecords(metadata, chunks, chunkIds, chunkEmbeddings);
        var docRecord = BuildDocRecord(metadata, summary, docEmbedding);
        await UpsertRecordsAsync(chunkRecords, docRecord, replaceExisting, ct);
    }

    private static async Task RemoveExistingDocumentAsync(
        string docId,
        IVectorStoreRecordCollection<string, ChunkRecord> chunkCollection,
        IVectorStoreRecordCollection<string, DocRecord> docCollection,
        CancellationToken ct)
    {
        await foreach (var chunk in chunkCollection.GetAsync(
                           record => record.DocId == docId,
                           top: 10_000,
                           cancellationToken: ct).ConfigureAwait(false))
        {
            await chunkCollection.DeleteAsync(chunk.ChunkId, ct).ConfigureAwait(false);
        }

        if (await docCollection.GetAsync(docId, cancellationToken: ct).ConfigureAwait(false) is not null)
        {
            await docCollection.DeleteAsync(docId, ct).ConfigureAwait(false);
        }
    }

    internal static List<ChunkRecord> BuildChunkRecords(
        DocumentMetadata metadata,
        IReadOnlyList<ChunkDraft> chunks,
        IReadOnlyList<string> chunkIds,
        IReadOnlyList<ReadOnlyMemory<float>> chunkEmbeddings)
    {
        var publishedOn = ToPublishedOn(metadata.PublishedOn);
        var records = new List<ChunkRecord>(chunks.Count);

        for (var index = 0; index < chunks.Count; index++)
        {
            var chunk = chunks[index];
            records.Add(new ChunkRecord
            {
                ChunkId = chunkIds[index],
                Text = chunk.Text,
                DocId = metadata.DocId,
                Title = metadata.Title,
                PageNumber = chunk.PageNumber,
                Section = chunk.Section,
                Cluster = metadata.Cluster,
                PublishedOn = publishedOn,
                Kind = chunk.Kind,
                Embedding = chunkEmbeddings[index]
            });
        }

        return records;
    }

    internal static DocRecord BuildDocRecord(
        DocumentMetadata metadata,
        DocumentSummaryResult summary,
        ReadOnlyMemory<float> docEmbedding = default) =>
        new()
        {
            DocId = metadata.DocId,
            Title = metadata.Title,
            Cluster = metadata.Cluster,
            PublishedOn = ToPublishedOn(metadata.PublishedOn),
            PageCount = metadata.PageCountApprox,
            Summary = summary.Summary,
            Topics = summary.Topics.ToList(),
            LineageGroup = metadata.LineageGroup,
            Embedding = docEmbedding
        };

    private static DateTimeOffset ToPublishedOn(DateOnly publishedOn) =>
        new(publishedOn, TimeOnly.MinValue, TimeSpan.Zero);
}
