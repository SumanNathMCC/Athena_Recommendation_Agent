using System.Text;
using Azure.AI.DocumentIntelligence;
using Athena.Core.Records;

namespace Athena.Ingestion.Extraction;

internal static class LayoutMarkdownBuilder
{
    private const float OcrConfidenceThreshold = 0.85f;
    private const string PreambleSectionHeader = "(preamble)";

    public static string BuildBody(AnalyzeResult result)
    {
        var layoutItems = CollectLayoutItems(result)
            .OrderBy(item => item.PageNumber)
            .ThenBy(item => item.TopY)
            .ToList();

        if (layoutItems.Count == 0)
        {
            return string.Empty;
        }

        var sections = GroupIntoSections(layoutItems);
        var builder = new StringBuilder();

        foreach (var section in sections)
        {
            AppendSection(builder, section);
        }

        return builder.ToString().TrimEnd();
    }

    private static List<LayoutItem> CollectLayoutItems(AnalyzeResult result)
    {
        var items = new List<LayoutItem>();

        foreach (var paragraph in result.Paragraphs)
        {
            if (!TryGetPrimaryRegion(paragraph.BoundingRegions, out var region) ||
                string.IsNullOrWhiteSpace(paragraph.Content))
            {
                continue;
            }

            var page = result.Pages.FirstOrDefault(candidate => candidate.PageNumber == region.PageNumber);
            var pageConfidence = GetPageConfidence(page);
            var proseKind = pageConfidence < OcrConfidenceThreshold ? "ocrprose" : "prose";
            var isHeading = IsHeading(paragraph);

            items.Add(new LayoutItem(
                region.PageNumber,
                TopY(region),
                LayoutItemKind.Paragraph,
                paragraph.Content.Trim(),
                proseKind,
                pageConfidence,
                isHeading));
        }

        foreach (var table in result.Tables)
        {
            if (!TryGetPrimaryRegion(table.BoundingRegions, out var region))
            {
                continue;
            }

            var markdown = TableMarkdownSerializer.ToMarkdown(table);
            if (string.IsNullOrWhiteSpace(markdown))
            {
                continue;
            }

            items.Add(new LayoutItem(
                region.PageNumber,
                TopY(region),
                LayoutItemKind.Table,
                markdown,
                "table",
                1.0f,
                IsHeading: false));
        }

        if (items.Count == 0)
        {
            foreach (var page in result.Pages.OrderBy(candidate => candidate.PageNumber))
            {
                var fallbackText = string.Join(
                    Environment.NewLine,
                    page.Lines
                        .Select(line => line.Content)
                        .Where(content => !string.IsNullOrWhiteSpace(content)));

                if (string.IsNullOrWhiteSpace(fallbackText))
                {
                    continue;
                }

                var pageConfidence = GetPageConfidence(page);
                var proseKind = pageConfidence < OcrConfidenceThreshold ? "ocrprose" : "prose";

                items.Add(new LayoutItem(
                    page.PageNumber,
                    0,
                    LayoutItemKind.Paragraph,
                    fallbackText.Trim(),
                    proseKind,
                    pageConfidence,
                    IsHeading: false));
            }
        }

        return items;
    }

    private static List<SectionDraft> GroupIntoSections(IReadOnlyList<LayoutItem> items)
    {
        var sections = new List<SectionDraft>();
        SectionDraft? current = null;

        foreach (var item in items)
        {
            if (item.IsHeading && item.ItemKind == LayoutItemKind.Paragraph)
            {
                current = new SectionDraft(item.Content, item.PageNumber);
                sections.Add(current);
                continue;
            }

            current ??= new SectionDraft(PreambleSectionHeader, item.PageNumber);
            if (!sections.Contains(current))
            {
                sections.Add(current);
            }

            current.Blocks.Add(item);
        }

        if (sections.Count == 0 && current is not null)
        {
            sections.Add(current);
        }

        return sections;
    }

    private static void AppendSection(StringBuilder builder, SectionDraft section)
    {
        var header = section.Header == PreambleSectionHeader ? string.Empty : section.Header;

        builder.AppendLine(
            $"<!-- section: header={YamlQuoted(header)} startPage={section.StartPage} -->");

        if (!string.IsNullOrWhiteSpace(header))
        {
            builder.AppendLine($"### {header}");
            builder.AppendLine();
        }

        foreach (var block in section.Blocks)
        {
            builder.AppendLine(
                $"<!-- block: page={block.PageNumber} kind={block.KindLabel} confidence={block.Confidence:0.###} -->");

            if (block.ItemKind == LayoutItemKind.Table)
            {
                builder.AppendLine();
                builder.AppendLine("### Table");
                builder.AppendLine();
            }

            builder.AppendLine(block.Content);
            builder.AppendLine();
        }
    }

    private static bool IsHeading(DocumentParagraph paragraph) =>
        paragraph.Role == ParagraphRole.SectionHeading ||
        paragraph.Role == ParagraphRole.Title ||
        HeadingHeuristics.IsLikelyHeading(paragraph.Content);

    private static float GetPageConfidence(DocumentPage? page)
    {
        if (page is null || page.Words.Count == 0)
        {
            return 1.0f;
        }

        return (float)page.Words.Average(word => word.Confidence);
    }

    private static bool TryGetPrimaryRegion(
        IReadOnlyList<BoundingRegion>? regions,
        out BoundingRegion region)
    {
        region = default;

        if (regions is null || regions.Count == 0)
        {
            return false;
        }

        region = regions
            .OrderBy(candidate => candidate.PageNumber)
            .ThenBy(candidate => TopY(candidate))
            .First();

        return true;
    }

    private static float TopY(BoundingRegion region)
    {
        if (region.Polygon is null || region.Polygon.Count < 2)
        {
            return 0;
        }

        return region.Polygon
            .Where((_, index) => index % 2 == 1)
            .DefaultIfEmpty(0)
            .Min();
    }

    private static string YamlQuoted(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        return value.Contains('\"', StringComparison.Ordinal) ||
               value.Contains(':', StringComparison.Ordinal)
            ? $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\""
            : $"\"{value}\"";
    }

    private sealed class SectionDraft(string header, int startPage)
    {
        public string Header { get; } = header;

        public int StartPage { get; } = startPage;

        public List<LayoutItem> Blocks { get; } = [];
    }

    private sealed record LayoutItem(
        int PageNumber,
        float TopY,
        LayoutItemKind ItemKind,
        string Content,
        string KindLabel,
        float Confidence,
        bool IsHeading);

    private enum LayoutItemKind
    {
        Paragraph,
        Table
    }
}
