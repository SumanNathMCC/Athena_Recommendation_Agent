using System.Text.RegularExpressions;
using Athena.Core.Records;

namespace Athena.Ingestion.Chunking;

public sealed class ChunkedMarkdownDocument
{
    public string DocId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Cluster { get; init; } = string.Empty;

    public DateOnly? PublishedOn { get; init; }

    public string Chunker { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<string> Topics { get; init; } = [];

    public DateTimeOffset? ChunkedAt { get; init; }

    public IReadOnlyList<ChunkedMarkdownEntry> Chunks { get; init; } = [];
}

public sealed class ChunkedMarkdownEntry
{
    public string ChunkId { get; init; } = string.Empty;

    public int PageNumber { get; init; }

    public string Section { get; init; } = string.Empty;

    public ChunkKind Kind { get; init; }

    public string Text { get; init; } = string.Empty;
}

public static class ChunkedMarkdownParser
{
    private static readonly Regex ChunkMarkerRegex = new(
        @"<!--\s*chunk:\s*id=(?<id>""(?:\\.|[^""\\])*""|[^""\s][^>]*?)\s+page=(?<page>\d+)\s+section=(?<section>""(?:\\.|[^""\\])*""|[^""\s][^>]*?)\s+kind=(?<kind>\w+)\s*-->",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static ChunkedMarkdownDocument Parse(string markdown)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(markdown);

        var (frontMatter, body) = SplitFrontMatter(markdown);
        var metadata = ParseFrontMatter(frontMatter);
        var topics = ParseTopics(frontMatter);
        var chunks = ParseChunks(body);

        return new ChunkedMarkdownDocument
        {
            DocId = metadata.GetValueOrDefault("docId", string.Empty),
            Title = metadata.GetValueOrDefault("title", string.Empty),
            Cluster = metadata.GetValueOrDefault("cluster", string.Empty),
            PublishedOn = metadata.TryGetValue("publishedOn", out var publishedOn) &&
                          DateOnly.TryParse(publishedOn, out var parsedDate)
                ? parsedDate
                : null,
            Chunker = metadata.GetValueOrDefault("chunker", string.Empty),
            Summary = metadata.GetValueOrDefault("summary", string.Empty),
            Topics = topics,
            ChunkedAt = metadata.TryGetValue("chunkedAt", out var chunkedAt) &&
                        DateTimeOffset.TryParse(chunkedAt, out var parsedAt)
                ? parsedAt
                : null,
            Chunks = chunks
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

    private static List<string> ParseTopics(string frontMatter)
    {
        var topics = new List<string>();
        var inTopics = false;

        foreach (var rawLine in frontMatter.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.StartsWith("topics:", StringComparison.OrdinalIgnoreCase))
            {
                inTopics = true;
                continue;
            }

            if (!inTopics)
            {
                continue;
            }

            if (!line.StartsWith("- ", StringComparison.Ordinal))
            {
                break;
            }

            topics.Add(line[2..].Trim().Trim('\"'));
        }

        return topics;
    }

    private static List<ChunkedMarkdownEntry> ParseChunks(string body)
    {
        var chunks = new List<ChunkedMarkdownEntry>();
        var matches = ChunkMarkerRegex.Matches(body);

        for (var index = 0; index < matches.Count; index++)
        {
            var match = matches[index];
            var contentStart = match.Index + match.Length;
            var contentEnd = index + 1 < matches.Count
                ? matches[index + 1].Index
                : body.Length;

            var text = body[contentStart..contentEnd].Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            chunks.Add(new ChunkedMarkdownEntry
            {
                ChunkId = Unquote(match.Groups["id"].Value),
                PageNumber = int.Parse(match.Groups["page"].Value),
                Section = Unquote(match.Groups["section"].Value),
                Kind = ParseKind(match.Groups["kind"].Value),
                Text = text
            });
        }

        return chunks;
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
