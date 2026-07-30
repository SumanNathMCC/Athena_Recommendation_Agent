using Athena.Core.Records;
using Athena.Ingestion.DocVectors;

namespace Athena.Tests.DocVectors;

public sealed class CentroidStrategyTests
{
    [Fact]
    public async Task BuildAsync_averages_chunk_embeddings()
    {
        var strategy = new CentroidStrategy();
        var doc = new DocRecord { DocId = "A1", Title = "Test" };
        var chunks = new List<ChunkRecord>
        {
            new() { ChunkId = "c1", Embedding = new float[] { 1f, 2f, 3f } },
            new() { ChunkId = "c2", Embedding = new float[] { 3f, 4f, 5f } }
        };

        var vector = await strategy.BuildAsync(doc, chunks);

        Assert.Equal(3, vector.Length);
        Assert.Equal(2f, vector.Span[0]);
        Assert.Equal(3f, vector.Span[1]);
        Assert.Equal(4f, vector.Span[2]);
    }

    [Fact]
    public async Task BuildAsync_throws_when_no_chunks()
    {
        var strategy = new CentroidStrategy();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            strategy.BuildAsync(new DocRecord(), Array.Empty<ChunkRecord>()));
    }
}
