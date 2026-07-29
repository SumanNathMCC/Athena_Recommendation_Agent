using Athena.Core.Records;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;

namespace Athena.Ingestion.VectorStore;

/// <summary>
/// Shared Semantic Kernel in-memory vector store for chunk and document collections.
/// </summary>
public sealed class CorpusVectorStore
{
    private readonly InMemoryVectorStore _vectorStore;
    private IVectorStoreRecordCollection<string, ChunkRecord>? _chunks;
    private IVectorStoreRecordCollection<string, DocRecord>? _documents;

    public CorpusVectorStore(InMemoryVectorStore vectorStore)
    {
        _vectorStore = vectorStore;
    }

    public async Task<IVectorStoreRecordCollection<string, ChunkRecord>> GetChunkCollectionAsync(
        CancellationToken ct = default)
    {
        if (_chunks is null)
        {
            _chunks = _vectorStore.GetCollection<string, ChunkRecord>(CorpusVectorStoreConstants.ChunksCollection);
            await _chunks.CreateCollectionIfNotExistsAsync(ct).ConfigureAwait(false);
        }

        return _chunks;
    }

    public async Task<IVectorStoreRecordCollection<string, DocRecord>> GetDocumentCollectionAsync(
        CancellationToken ct = default)
    {
        if (_documents is null)
        {
            _documents = _vectorStore.GetCollection<string, DocRecord>(CorpusVectorStoreConstants.DocumentsCollection);
            await _documents.CreateCollectionIfNotExistsAsync(ct).ConfigureAwait(false);
        }

        return _documents;
    }
}
