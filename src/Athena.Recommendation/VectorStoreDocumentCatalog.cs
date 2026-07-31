using Athena.Core.Records;
using Athena.Ingestion.VectorStore;

namespace Athena.Recommendation;

public interface IDocumentCatalog
{
    Task<IReadOnlyList<DocRecord>> GetAllAsync(CancellationToken ct = default);

    Task<DocRecord?> FindByIdOrTitleAsync(string idOrTitle, CancellationToken ct = default);
}

/// <summary>
/// Reads document records from the in-memory vector store collection.
/// </summary>
public sealed class VectorStoreDocumentCatalog : IDocumentCatalog
{
    private readonly CorpusVectorStore _vectorStore;

    public VectorStoreDocumentCatalog(CorpusVectorStore vectorStore)
    {
        _vectorStore = vectorStore;
    }

    public async Task<IReadOnlyList<DocRecord>> GetAllAsync(CancellationToken ct = default)
    {
        var collection = await _vectorStore.GetDocumentCollectionAsync(ct).ConfigureAwait(false);
        var docs = new List<DocRecord>();

        await foreach (var doc in collection.GetAsync(
                           record => record.DocId != string.Empty,
                           top: 500,
                           cancellationToken: ct).ConfigureAwait(false))
        {
            docs.Add(doc);
        }

        return docs;
    }

    public async Task<DocRecord?> FindByIdOrTitleAsync(string idOrTitle, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idOrTitle))
        {
            return null;
        }

        var key = idOrTitle.Trim();
        var collection = await _vectorStore.GetDocumentCollectionAsync(ct).ConfigureAwait(false);

        var byId = await collection.GetAsync(key, cancellationToken: ct).ConfigureAwait(false);
        if (byId is not null)
        {
            return byId;
        }

        // Case-insensitive id / exact title / contains title.
        var all = await GetAllAsync(ct).ConfigureAwait(false);
        var exactId = all.FirstOrDefault(d =>
            string.Equals(d.DocId, key, StringComparison.OrdinalIgnoreCase));
        if (exactId is not null)
        {
            return exactId;
        }

        var exactTitle = all.FirstOrDefault(d =>
            string.Equals(d.Title, key, StringComparison.OrdinalIgnoreCase));
        if (exactTitle is not null)
        {
            return exactTitle;
        }

        return all.FirstOrDefault(d =>
            d.Title.Contains(key, StringComparison.OrdinalIgnoreCase) ||
            key.Contains(d.Title, StringComparison.OrdinalIgnoreCase));
    }
}
