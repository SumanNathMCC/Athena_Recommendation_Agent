namespace Athena.Ingestion.Chunking;

public interface IChunkerFactory
{
    IChunker GetChunker(ChunkingStrategy strategy);
}

public sealed class ChunkerFactory : IChunkerFactory
{
    private readonly FixedWindowChunker _fixedWindowChunker;
    private readonly SectionAwareChunker _sectionAwareChunker;

    public ChunkerFactory(FixedWindowChunker fixedWindowChunker, SectionAwareChunker sectionAwareChunker)
    {
        _fixedWindowChunker = fixedWindowChunker;
        _sectionAwareChunker = sectionAwareChunker;
    }

    public IChunker GetChunker(ChunkingStrategy strategy) =>
        strategy switch
        {
            ChunkingStrategy.FixedWindow => _fixedWindowChunker,
            _ => _sectionAwareChunker
        };
}
