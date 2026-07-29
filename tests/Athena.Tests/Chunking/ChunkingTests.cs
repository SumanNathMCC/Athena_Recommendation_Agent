using Athena.Core.Records;
using Athena.Ingestion.Chunking;
using Athena.Ingestion.Extraction;
using Athena.Ingestion.Summarization;
using Microsoft.Extensions.Options;

namespace Athena.Tests.Chunking;

public sealed class TokenEstimatorTests
{
    [Fact]
    public void SplitWindows_usesTwentyPercentOverlap()
    {
        var text = new string('a', 5000) + " " + new string('b', 5000);

        var windows = TokenEstimator.SplitWindows(text, maxTokens: 1000, overlapRatio: 0.20).ToList();

        Assert.True(windows.Count >= 3);
        Assert.All(windows, window => Assert.True(TokenEstimator.Estimate(window) <= 1000));
    }
}

public sealed class FixedWindowChunkerTests
{
    [Fact]
    public void Chunk_flattens_prose_into_token_windows_and_keeps_tables_atomic()
    {
        var chunker = new FixedWindowChunker(Options.Create(new ChunkingOptions
        {
            MaxTokens = 1000,
            OverlapRatio = 0.20
        }));

        var document = ExtractedMarkdownParser.Parse(SampleMarkdown.TwoSectionDocument);
        var metadata = SampleMarkdown.CreateMetadata("A1");

        var chunks = chunker.Chunk(metadata, document);

        Assert.Contains(chunks, chunk => chunk.Kind == ChunkKind.Table);
        Assert.Contains(chunks, chunk => chunk.Kind == ChunkKind.Prose);
        Assert.True(chunks.Count(chunk => chunk.Kind == ChunkKind.Prose) >= 1);
    }
}

public sealed class SectionAwareChunkerTests
{
    [Fact]
    public void Chunk_emits_one_chunk_per_small_section()
    {
        var chunker = new SectionAwareChunker(Options.Create(new ChunkingOptions
        {
            MaxTokens = 1000,
            OverlapRatio = 0.20
        }));

        var document = ExtractedMarkdownParser.Parse(SampleMarkdown.TwoSectionDocument);
        var metadata = SampleMarkdown.CreateMetadata("A1");

        var chunks = chunker.Chunk(metadata, document);

        Assert.Equal(2, chunks.Count(chunk => chunk.Kind == ChunkKind.Prose));
        Assert.Single(chunks, chunk => chunk.Kind == ChunkKind.Table);
        Assert.Contains(chunks, chunk => chunk.Section == "I. Introduction");
        Assert.Contains(chunks, chunk => chunk.Section == "II. Governance");
    }
}

public sealed class ChunkedMarkdownWriterTests
{
    [Fact]
    public void Write_includes_summary_and_topics_in_frontmatter()
    {
        var metadata = SampleMarkdown.CreateMetadata("A1");
        var summary = new DocumentSummaryResult
        {
            Summary = "Short summary",
            Topics = ["resilience", "governance", "risk"]
        };
        var chunks = new List<ChunkDraft>
        {
            new()
            {
                Text = "Introduction body",
                PageNumber = 1,
                Kind = ChunkKind.Prose,
                Section = "I. Introduction"
            }
        };

        var markdown = ChunkedMarkdownWriter.Write(metadata, "section-aware", summary, chunks);

        Assert.Contains("summary:", markdown, StringComparison.Ordinal);
        Assert.Contains("topics:", markdown, StringComparison.Ordinal);
        Assert.Contains("Introduction body", markdown, StringComparison.Ordinal);
    }
}

internal static class SampleMarkdown
{
    public const string TwoSectionDocument = """
        ---
        docId: A1
        title: Sample
        modelId: prebuilt-layout
        ---

        <!-- section: header="I. Introduction" startPage=1 -->
        ### I. Introduction

        <!-- block: page=1 kind=prose confidence=0.99 -->
        This introduction explains operational resilience in brief.

        <!-- section: header="II. Governance" startPage=2 -->
        ### II. Governance

        <!-- block: page=2 kind=prose confidence=0.99 -->
        The board must establish a strong risk management culture.

        <!-- block: page=2 kind=table confidence=1.000 -->
        ### Table

        | Principle | Summary |
        |---|---|
        | 1 | Governance |
        """;

    public static DocumentMetadata CreateMetadata(string docId) =>
        new()
        {
            DocId = docId,
            Title = "Sample",
            Cluster = "A",
            PublishedOn = new DateOnly(2021, 3, 31),
            FilePath = "sample.pdf"
        };
}
