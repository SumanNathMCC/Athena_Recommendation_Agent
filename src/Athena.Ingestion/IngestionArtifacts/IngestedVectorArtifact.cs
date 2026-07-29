using Athena.Core.Records;

namespace Athena.Ingestion.IngestionArtifacts;

public sealed class IngestedVectorArtifact
{
    public string DocId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Cluster { get; set; } = string.Empty;

    public DateOnly PublishedOn { get; set; }

    public string? LineageGroup { get; set; }

    public int PageCount { get; set; }

    public string Summary { get; set; } = string.Empty;

    public IList<string> Topics { get; set; } = [];

    public string Chunker { get; set; } = string.Empty;

    public DateTimeOffset IngestedAt { get; set; }

    public float[] DocEmbedding { get; set; } = [];

    public List<IngestedChunkVector> Chunks { get; set; } = [];
}

public sealed class IngestedChunkVector
{
    public string ChunkId { get; set; } = string.Empty;

    public int PageNumber { get; set; }

    public string Section { get; set; } = string.Empty;

    public ChunkKind Kind { get; set; }

    public float[] Embedding { get; set; } = [];
}
