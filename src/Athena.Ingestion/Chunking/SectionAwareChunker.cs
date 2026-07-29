using Athena.Core.Records;
using Athena.Ingestion.Extraction;
using Microsoft.Extensions.Options;

namespace Athena.Ingestion.Chunking;

public sealed class SectionAwareChunker : IChunker
{
    private readonly ChunkingOptions _options;

    public SectionAwareChunker(IOptions<ChunkingOptions> options)
    {
        _options = options.Value;
    }

    public string Name => "section-aware";

    public IReadOnlyList<ChunkDraft> Chunk(
        DocumentMetadata metadata,
        ExtractedMarkdownDocument document)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(document);

        var chunks = new List<ChunkDraft>();

        foreach (var section in document.Sections)
        {
            var sectionHeader = section.Header;
            var proseBuffer = new List<ExtractedMarkdownBlock>();

            foreach (var block in section.Blocks)
            {
                if (block.Kind == ChunkKind.Table)
                {
                    FlushProseSection(chunks, proseBuffer, sectionHeader, section.StartPage);
                    chunks.Add(new ChunkDraft
                    {
                        Text = block.Markdown,
                        PageNumber = block.PageNumber,
                        Kind = ChunkKind.Table,
                        Section = sectionHeader
                    });
                    continue;
                }

                proseBuffer.Add(block);
            }

            FlushProseSection(chunks, proseBuffer, sectionHeader, section.StartPage);
        }

        return chunks;
    }

    private void FlushProseSection(
        List<ChunkDraft> chunks,
        List<ExtractedMarkdownBlock> proseBuffer,
        string sectionHeader,
        int startPage)
    {
        if (proseBuffer.Count == 0)
        {
            return;
        }

        var proseText = string.Join(
            "\n\n",
            proseBuffer.Select(block => block.Markdown.Trim()));

        var tokenCount = TokenEstimator.Estimate(proseText);
        var kind = proseBuffer.Any(block => block.Kind == ChunkKind.OcrProse)
            ? ChunkKind.OcrProse
            : ChunkKind.Prose;

        if (tokenCount <= _options.MaxTokens)
        {
            chunks.Add(new ChunkDraft
            {
                Text = proseText,
                PageNumber = proseBuffer[0].PageNumber,
                Kind = kind,
                Section = sectionHeader
            });
        }
        else
        {
            foreach (var window in TokenEstimator.SplitWindows(
                         proseText,
                         _options.MaxTokens,
                         _options.OverlapRatio))
            {
                chunks.Add(new ChunkDraft
                {
                    Text = window,
                    PageNumber = ResolvePageNumber(window, proseBuffer, startPage),
                    Kind = kind,
                    Section = sectionHeader
                });
            }
        }

        proseBuffer.Clear();
    }

    private static int ResolvePageNumber(
        string window,
        IReadOnlyList<ExtractedMarkdownBlock> blocks,
        int fallbackPage)
    {
        var anchor = window.Length >= 40 ? window[..40] : window;
        var match = blocks.FirstOrDefault(block => block.Markdown.Contains(anchor, StringComparison.Ordinal));
        return match?.PageNumber ?? fallbackPage;
    }
}
