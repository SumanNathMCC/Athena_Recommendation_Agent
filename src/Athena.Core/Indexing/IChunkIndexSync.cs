using Athena.Core.Records;

namespace Athena.Core.Indexing;

/// <summary>
/// Keeps secondary indexes (e.g. Lucene) in sync with the chunk vector store.
/// </summary>
public interface IChunkIndexSync
{
    void Upsert(IReadOnlyList<ChunkRecord> chunks);

    void RemoveByDocId(string docId);
}
