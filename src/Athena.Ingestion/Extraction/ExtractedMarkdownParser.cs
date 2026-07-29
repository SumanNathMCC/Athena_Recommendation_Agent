using System.Text.RegularExpressions;
using Athena.Core.Records;

namespace Athena.Ingestion.Extraction;

public sealed class ExtractedMarkdownDocument
{
    public string DocId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string? SourcePdf { get; init; }

    public DateTimeOffset? ExtractedAt { get; init; }

    public string? ModelId { get; init; }

    public IReadOnlyList<ExtractedMarkdownSection> Sections { get; init; } = [];

    public IReadOnlyList<ExtractedMarkdownBlock> Blocks =>
        Sections.SelectMany(section => section.Blocks).ToList();
}

public sealed class ExtractedMarkdownSection
{
    public string Header { get; init; } = string.Empty;

    public int StartPage { get; init; }

    public IReadOnlyList<ExtractedMarkdownBlock> Blocks { get; init; } = [];
}

public sealed class ExtractedMarkdownBlock
{
    public int PageNumber { get; init; }

    public ChunkKind Kind { get; init; }

    public float MeanConfidence { get; init; }

    public string Markdown { get; init; } = string.Empty;

    public string SectionHeader { get; init; } = string.Empty;
}

public static class ExtractedMarkdownParser
{
    private static readonly Regex SectionMarkerRegex = new(
        @"<!--\s*section:\s*header=(?<header>""(?:\\.|[^""\\])*""|[^""\s][^>]*?)\s+startPage=(?<startPage>\d+)\s*-->",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex BlockMarkerRegex = new(
        @"<!--\s*block:\s*page=(?<page>\d+)\s+kind=(?<kind>\w+)\s+confidence=(?<confidence>[\d.]+)\s*-->",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static ExtractedMarkdownDocument Parse(string markdown)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(markdown);

        var (frontMatter, body) = SplitFrontMatter(markdown);
        var metadata = ParseFrontMatter(frontMatter);
        var sections = ParseSections(body);

        return new ExtractedMarkdownDocument
        {
            DocId = metadata.GetValueOrDefault("docId", string.Empty),
            Title = metadata.GetValueOrDefault("title", string.Empty),
            SourcePdf = metadata.GetValueOrDefault("sourcePdf"),
            ExtractedAt = metadata.TryGetValue("extractedAt", out var extractedAt) &&
                          DateTimeOffset.TryParse(extractedAt, out var parsedAt)
                ? parsedAt
                : null,
            ModelId = metadata.GetValueOrDefault("modelId"),
            Sections = sections
        };
    }

    private static (string FrontMatter, string Body) SplitFrontMatter(string markdown)
    {
        if (!markdown.StartsWith("---", StringComparison.Ordinal))
        {
            return (string.Empty, markdown);
        }

        var endIndex = markdown.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (endIndex < 0)
        {
            return (string.Empty, markdown);
        }

        var frontMatter = markdown[4..endIndex];
        var body = markdown[(endIndex + 4)..].TrimStart('\r', '\n');
        return (frontMatter, body);
    }

    private static Dictionary<string, string> ParseFrontMatter(string frontMatter)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in frontMatter.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim().Trim('"');
            values[key] = value;
        }

        return values;
    }

    private static List<ExtractedMarkdownSection> ParseSections(string body)
    {
        var sections = new List<ExtractedMarkdownSection>();
        var sectionMatches = SectionMarkerRegex.Matches(body);

        if (sectionMatches.Count == 0)
        {
            var legacyBlocks = ParseBlocks(body, string.Empty);
            if (legacyBlocks.Count > 0)
            {
                sections.Add(new ExtractedMarkdownSection
                {
                    Header = string.Empty,
                    StartPage = legacyBlocks[0].PageNumber,
                    Blocks = legacyBlocks
                });
            }

            return sections;
        }

        for (var index = 0; index < sectionMatches.Count; index++)
        {
            var match = sectionMatches[index];
            var contentStart = match.Index + match.Length;
            var contentEnd = index + 1 < sectionMatches.Count
                ? sectionMatches[index + 1].Index
                : body.Length;

            var sectionBody = body[contentStart..contentEnd];
            var header = Unquote(match.Groups["header"].Value);
            var startPage = int.Parse(match.Groups["startPage"].Value);
            var blocks = ParseBlocks(sectionBody, header);

            sections.Add(new ExtractedMarkdownSection
            {
                Header = header,
                StartPage = startPage,
                Blocks = blocks
            });
        }

        return sections;
    }

    private static List<ExtractedMarkdownBlock> ParseBlocks(string body, string sectionHeader)
    {
        var blocks = new List<ExtractedMarkdownBlock>();
        var matches = BlockMarkerRegex.Matches(body);

        for (var index = 0; index < matches.Count; index++)
        {
            var match = matches[index];
            var contentStart = match.Index + match.Length;
            var contentEnd = index + 1 < matches.Count
                ? matches[index + 1].Index
                : body.Length;

            var content = body[contentStart..contentEnd].Trim();
            content = StripSectionHeadingLine(content);

            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            blocks.Add(new ExtractedMarkdownBlock
            {
                PageNumber = int.Parse(match.Groups["page"].Value),
                Kind = ParseKind(match.Groups["kind"].Value),
                MeanConfidence = float.Parse(match.Groups["confidence"].Value),
                Markdown = content,
                SectionHeader = sectionHeader
            });
        }

        return blocks;
    }

    private static string StripSectionHeadingLine(string content)
    {
        var lines = content.Split('\n').ToList();

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
        {
            lines.RemoveAt(0);
        }

        if (lines.Count > 0 && lines[0].StartsWith("### ", StringComparison.Ordinal))
        {
            lines.RemoveAt(0);

            while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
            {
                lines.RemoveAt(0);
            }
        }

        if (lines.Count > 0 && lines[0].StartsWith("## Page ", StringComparison.Ordinal))
        {
            lines.RemoveAt(0);

            while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
            {
                lines.RemoveAt(0);
            }
        }

        return string.Join(Environment.NewLine, lines).Trim();
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 && value.StartsWith('"') && value.EndsWith('"'))
        {
            return value[1..^1].Replace("\\\"", "\"", StringComparison.Ordinal);
        }

        return value;
    }

    private static ChunkKind ParseKind(string kind) =>
        kind.ToLowerInvariant() switch
        {
            "table" => ChunkKind.Table,
            "ocrprose" => ChunkKind.OcrProse,
            _ => ChunkKind.Prose
        };
}
