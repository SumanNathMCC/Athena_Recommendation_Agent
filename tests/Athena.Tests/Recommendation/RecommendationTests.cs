using Athena.Core.Records;
using Athena.Recommendation;

namespace Athena.Tests.Recommendation;

public sealed class MmrDiversifierTests
{
    [Fact]
    public void Select_lambdaOne_picksPureRelevanceOrder()
    {
        var seed = Vec(1, 0);
        var candidates = new List<DocRecord>
        {
            Doc("far", Vec(0, 1)),
            Doc("near", Vec(0.9f, 0.1f)),
            Doc("mid", Vec(0.6f, 0.4f))
        };

        var selected = new MmrDiversifier().Select(seed, candidates, topK: 2, lambda: 1.0);

        Assert.Equal(["near", "mid"], selected.Select(d => d.DocId).ToArray());
    }

    [Fact]
    public void Select_lambdaZero_prefersDiversityAgainstAlreadySelected()
    {
        var seed = Vec(1, 0);
        // near is closest to seed; twin is almost identical to near; far is orthogonal.
        var candidates = new List<DocRecord>
        {
            Doc("near", Vec(1f, 0f)),
            Doc("twin", Vec(0.99f, 0.01f)),
            Doc("far", Vec(0f, 1f))
        };

        var selected = new MmrDiversifier().Select(seed, candidates, topK: 2, lambda: 0.0);

        Assert.Equal(2, selected.Count);
        Assert.Contains(selected, d => d.DocId == "near");
        Assert.Contains(selected, d => d.DocId == "far");
        Assert.DoesNotContain(selected, d => d.DocId == "twin");
    }

    [Fact]
    public void Select_emptyCandidates_returnsEmpty()
    {
        var selected = new MmrDiversifier().Select(Vec(1, 0), [], topK: 3);
        Assert.Empty(selected);
    }

    private static DocRecord Doc(string id, ReadOnlyMemory<float> embedding) =>
        new()
        {
            DocId = id,
            Title = id,
            Embedding = embedding,
            Topics = ["t"],
            PublishedOn = DateTimeOffset.UtcNow
        };

    private static ReadOnlyMemory<float> Vec(float x, float y) => new[] { x, y };
}

public sealed class LineageNearDuplicateResolverTests
{
    [Fact]
    public void Resolve_keepsNewestInLineageGroup()
    {
        var ranked = new List<DocRecord>
        {
            Doc("A2", "bcbs-op", published: new DateTimeOffset(2020, 8, 1, 0, 0, 0, TimeSpan.Zero)),
            Doc("A1", "bcbs-op", published: new DateTimeOffset(2021, 3, 1, 0, 0, 0, TimeSpan.Zero)),
            Doc("B1", lineage: null, published: DateTimeOffset.UtcNow)
        };

        var resolved = new LineageNearDuplicateResolver(similarityCeiling: 1.1) // disable cosine collapse
            .Resolve(ranked);

        Assert.Equal(["A1", "B1"], resolved.Select(d => d.DocId).ToArray());
    }

    [Fact]
    public void Resolve_excludesOtherMembersOfSeedLineage()
    {
        var seed = Doc("A1", "bcbs-op", published: new DateTimeOffset(2021, 3, 1, 0, 0, 0, TimeSpan.Zero));
        var ranked = new List<DocRecord>
        {
            Doc("A2", "bcbs-op", published: new DateTimeOffset(2020, 8, 1, 0, 0, 0, TimeSpan.Zero)),
            Doc("B1", lineage: null, published: DateTimeOffset.UtcNow)
        };

        var resolved = new LineageNearDuplicateResolver(similarityCeiling: 1.1)
            .Resolve(ranked, seed);

        Assert.Single(resolved);
        Assert.Equal("B1", resolved[0].DocId);
    }

    [Fact]
    public void Resolve_similarityCeiling_collapsesUnlabeledTwins()
    {
        var v = new float[] { 1f, 0f, 0f };
        var ranked = new List<DocRecord>
        {
            Doc("X", lineage: null, published: DateTimeOffset.UtcNow, embedding: v),
            Doc("Y", lineage: null, published: DateTimeOffset.UtcNow, embedding: v),
            Doc("Z", lineage: null, published: DateTimeOffset.UtcNow, embedding: new float[] { 0f, 1f, 0f })
        };

        var resolved = new LineageNearDuplicateResolver(similarityCeiling: 0.97)
            .Resolve(ranked);

        Assert.Equal(2, resolved.Count);
        Assert.Equal("X", resolved[0].DocId);
        Assert.Equal("Z", resolved[1].DocId);
    }

    private static DocRecord Doc(
        string id,
        string? lineage,
        DateTimeOffset published,
        ReadOnlyMemory<float>? embedding = null) =>
        new()
        {
            DocId = id,
            Title = id,
            LineageGroup = lineage,
            PublishedOn = published,
            Embedding = embedding ?? new float[] { Random.Shared.NextSingle(), Random.Shared.NextSingle(), 0.1f },
            Topics = ["t"]
        };
}

public sealed class SessionInterestProfileStoreTests
{
    [Fact]
    public async Task UpdateAsync_blendsWithDecay()
    {
        var store = new SessionInterestProfileStore();
        var first = new float[] { 1f, 0f };
        var second = new float[] { 0f, 1f };

        await store.UpdateAsync("s1", first, decay: 0.8);
        var profile = await store.UpdateAsync("s1", second, decay: 0.8);

        Assert.Equal(0.8f, profile.Span[0], precision: 5);
        Assert.Equal(0.2f, profile.Span[1], precision: 5);
    }

    [Fact]
    public async Task MarkSurfaced_isTrackedPerSession()
    {
        var store = new SessionInterestProfileStore();
        await store.MarkSurfacedAsync("s1", ["A1", "B1"]);
        var surfaced = await store.GetAlreadySurfacedAsync("s1");
        Assert.True(surfaced.Contains("A1"));
        Assert.False((await store.GetAlreadySurfacedAsync("s2")).Contains("A1"));
    }
}
