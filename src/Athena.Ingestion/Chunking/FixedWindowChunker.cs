using Athena.Core.Records;
using Athena.Ingestion.Extraction;
using Microsoft.Extensions.Options;

namespace Athena.Ingestion.Chunking;

public sealed class FixedWindowChunker : IChunker
{
    private readonly ChunkingOptions _options;

    public FixedWindowChunker(IOptions<ChunkingOptions> options)
    {
        _options = options.Value;
    }

    public string Name => "fixed-window";

    public IReadOnlyList<ChunkDraft> Chunk(
        DocumentMetadata metadata,
        ExtractedMarkdownDocument document)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(document);

        var chunks = new List<ChunkDraft>();
        var proseBuffer = new List<ExtractedMarkdownBlock>();

        foreach (var block in document.Blocks)
        {
            if (block.Kind == ChunkKind.Table)
            {
                FlushProseBuffer(chunks, proseBuffer);
                chunks.Add(new ChunkDraft
                {
                    Text = block.Markdown,
                    PageNumber = block.PageNumber,
                    Kind = ChunkKind.Table,
                    Section = block.SectionHeader
                });
                continue;
            }

            proseBuffer.Add(block);
        }

        FlushProseBuffer(chunks, proseBuffer);
        return chunks;
    }

    private void FlushProseBuffer(List<ChunkDraft> chunks, List<ExtractedMarkdownBlock> proseBuffer)
    {
        if (proseBuffer.Count == 0)
        {
            return;
        }

        var proseText = string.Join(
            "\n\n",
            proseBuffer.Select(block => block.Markdown.Trim()));

        var section = proseBuffer[0].SectionHeader;
        var fallbackPage = proseBuffer[0].PageNumber;
        var kind = proseBuffer.Any(block => block.Kind == ChunkKind.OcrProse)
            ? ChunkKind.OcrProse
            : ChunkKind.Prose;

        foreach (var window in TokenEstimator.SplitWindows(
                     proseText,
                     _options.MaxTokens,
                     _options.OverlapRatio))
        {
            chunks.Add(new ChunkDraft
            {
                Text = window,
                PageNumber = ResolvePageNumber(window, proseBuffer, fallbackPage),
                Kind = kind,
                Section = section
            });
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
