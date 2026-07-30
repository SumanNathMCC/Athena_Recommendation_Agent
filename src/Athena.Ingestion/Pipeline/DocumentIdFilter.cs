namespace Athena.Ingestion.Pipeline;

internal static class DocumentIdFilter
{
    /// <summary>
    /// null = all documents; empty = none; otherwise case-insensitive match on DocId.
    /// </summary>
    public static IReadOnlyList<T> Apply<T>(
        IEnumerable<T> documents,
        Func<T, string> docIdSelector,
        IReadOnlyCollection<string>? docIds)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(docIdSelector);

        if (docIds is null)
        {
            return documents as IReadOnlyList<T> ?? documents.ToList();
        }

        if (docIds.Count == 0)
        {
            return Array.Empty<T>();
        }

        var selected = new HashSet<string>(docIds, StringComparer.OrdinalIgnoreCase);
        return documents.Where(document => selected.Contains(docIdSelector(document))).ToList();
    }
}
