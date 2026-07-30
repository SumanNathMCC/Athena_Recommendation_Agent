using Athena.Retrieval;

namespace Athena.Tests.Retrieval;

public sealed class ReciprocalRankFusionTests
{
    [Fact]
    public void Fuse_UsesRankNotRawScores()
    {
        var dense = new List<Passage>
        {
            P("a", score: 0.99),
            P("b", score: 0.50),
            P("c", score: 0.10)
        };

        var lexical = new List<Passage>
        {
            P("c", score: 100),
            P("b", score: 50),
            P("d", score: 10)
        };

        var fused = ReciprocalRankFusion.Fuse([dense, lexical], topN: 4, k: 60);

        Assert.Equal(4, fused.Count);
        // c: dense rank 3 + lexical rank 1 → 1/63 + 1/61 (beats b's 1/62+1/62)
        // b: dense rank 2 + lexical rank 2 → 1/62 + 1/62
        // a: dense rank 1 only → 1/61
        // d: lexical rank 3 only → 1/63
        Assert.Equal("c", fused[0].ChunkId);
        Assert.Equal("b", fused[1].ChunkId);
        Assert.Equal("a", fused[2].ChunkId);
        Assert.Equal("d", fused[3].ChunkId);

        var expectedC = 1.0 / 63 + 1.0 / 61;
        Assert.Equal(expectedC, fused[0].Score, precision: 10);
    }

    [Fact]
    public void Fuse_EmptyLists_ReturnsEmpty()
    {
        var fused = ReciprocalRankFusion.Fuse(
            [Array.Empty<Passage>(), Array.Empty<Passage>()],
            topN: 6);
        Assert.Empty(fused);
    }

    [Fact]
    public void Fuse_SingleList_PreservesRankOrder()
    {
        var only = new List<Passage> { P("x"), P("y"), P("z") };
        var fused = ReciprocalRankFusion.Fuse([only], topN: 2, k: 60);
        Assert.Equal(["x", "y"], fused.Select(p => p.ChunkId).ToArray());
        Assert.Equal(1.0 / 61, fused[0].Score, precision: 10);
        Assert.Equal(1.0 / 62, fused[1].Score, precision: 10);
    }

    private static Passage P(string id, double score = 0) =>
        new(id, "D1", "Title", 1, "Sec", $"text-{id}", score);
}
