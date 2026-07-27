using Athena.Core.Records;

namespace Athena.Ingestion.Injection;

public sealed class InjectedDocument
{
    public string DocId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public DateTimeOffset InjectedAt { get; set; }

    public List<InjectedChunk> Chunks { get; set; } = [];
}

public sealed class InjectedChunk
{
    public string ChunkId { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public int PageNumber { get; set; }

    public ChunkKind Kind { get; set; }
}
