using Athena.Core.Records;
using Athena.Ingestion.Extraction;

namespace Athena.Ingestion.Chunking;

public sealed class ChunkingOptions
{
    public const string SectionName = "Chunking";

    public int MaxTokens { get; set; } = 800;

    public double OverlapRatio { get; set; } = 0.20;

    public ChunkingStrategy Strategy { get; set; } = ChunkingStrategy.SectionAware;
}

public enum ChunkingStrategy
{
    FixedWindow,
    SectionAware
}

public interface IChunker
{
    string Name { get; }

    IReadOnlyList<ChunkDraft> Chunk(
        DocumentMetadata metadata,
        ExtractedMarkdownDocument document);
}

public sealed class ChunkDraft
{
    public required string Text { get; init; }

    public required int PageNumber { get; init; }

    public required ChunkKind Kind { get; init; }

    public required string Section { get; init; }
}
