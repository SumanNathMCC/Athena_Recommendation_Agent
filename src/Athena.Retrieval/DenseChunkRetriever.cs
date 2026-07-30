using Athena.Core.Records;
using Athena.Ingestion.Embeddings;
using Athena.Ingestion.VectorStore;
using Microsoft.Extensions.VectorData;

namespace Athena.Retrieval;

/// <summary>
/// Dense (embedding) retrieval over the in-memory chunk collection.
/// </summary>
public sealed class DenseChunkRetriever : IDenseRetriever
{
    private readonly CorpusVectorStore _vectorStore;
    private readonly ICorpusEmbeddingService _embeddings;

    public DenseChunkRetriever(
        CorpusVectorStore vectorStore,
        ICorpusEmbeddingService embeddings)
    {
        _vectorStore = vectorStore;
        _embeddings = embeddings;
    }

    public async Task<IReadOnlyList<Passage>> SearchAsync(
        string query,
        int topK = 20,
        string? docId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query) || topK <= 0)
        {
            return Array.Empty<Passage>();
        }

        var collection = await _vectorStore.GetChunkCollectionAsync(ct).ConfigureAwait(false);
        var embedding = await _embeddings.EmbedAsync(query, ct).ConfigureAwait(false);

        VectorSearchOptions<ChunkRecord> options;
        if (!string.IsNullOrWhiteSpace(docId))
        {
            var filterDocId = docId.Trim();
            options = new VectorSearchOptions<ChunkRecord>
            {
                Filter = record => record.DocId == filterDocId
            };
        }
        else
        {
            options = new VectorSearchOptions<ChunkRecord>();
        }

        var passages = new List<Passage>(topK);
        await foreach (var result in collection.SearchEmbeddingAsync(embedding, topK, options, ct)
                           .ConfigureAwait(false))
        {
            var record = result.Record;
            passages.Add(new Passage(
                record.ChunkId,
                record.DocId,
                record.Title,
                record.PageNumber,
                record.Section,
                record.Text,
                result.Score ?? 0d));
        }

        return passages;
    }
}
