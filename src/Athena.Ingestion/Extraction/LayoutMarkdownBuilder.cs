using System.Text;
using Azure.AI.DocumentIntelligence;
using Athena.Core.Records;

namespace Athena.Ingestion.Extraction;

internal static class LayoutMarkdownBuilder
{
    private const float OcrConfidenceThreshold = 0.85f;

    public static string BuildBody(AnalyzeResult result)
    {
        var pageNumbers = result.Pages
            .Select(page => page.PageNumber)
            .Distinct()
            .OrderBy(number => number)
            .ToList();

        var builder = new StringBuilder();

        foreach (var pageNumber in pageNumbers)
        {
            var page = result.Pages.FirstOrDefault(candidate => candidate.PageNumber == pageNumber);
            var pageConfidence = GetPageConfidence(page);
            var proseKind = pageConfidence < OcrConfidenceThreshold
                ? "ocrprose"
                : "prose";

            var layoutItems = new List<LayoutItem>();

            foreach (var paragraph in result.Paragraphs)
            {
                if (!TryGetRegionOnPage(paragraph.BoundingRegions, pageNumber, out var region) ||
                    string.IsNullOrWhiteSpace(paragraph.Content))
                {
                    continue;
                }

                layoutItems.Add(new LayoutItem(
                    TopY(region),
                    LayoutItemKind.Paragraph,
                    FormatParagraph(paragraph),
                    proseKind,
                    pageConfidence));
            }

            foreach (var table in result.Tables)
            {
                if (!TryGetRegionOnPage(table.BoundingRegions, pageNumber, out var region))
                {
                    continue;
                }

                var markdown = TableMarkdownSerializer.ToMarkdown(table);
                if (string.IsNullOrWhiteSpace(markdown))
                {
                    continue;
                }

                layoutItems.Add(new LayoutItem(
                    TopY(region),
                    LayoutItemKind.Table,
                    markdown,
                    "table",
                    1.0f));
            }

            if (layoutItems.Count == 0 && page is not null)
            {
                var fallbackText = string.Join(
                    Environment.NewLine,
                    page.Lines
                        .Select(line => line.Content)
                        .Where(content => !string.IsNullOrWhiteSpace(content)));

                if (!string.IsNullOrWhiteSpace(fallbackText))
                {
                    layoutItems.Add(new LayoutItem(
                        0,
                        LayoutItemKind.Paragraph,
                        fallbackText.Trim(),
                        proseKind,
                        pageConfidence));
                }
            }

            if (layoutItems.Count == 0)
            {
                continue;
            }

            builder.AppendLine($"## Page {pageNumber}");
            builder.AppendLine();

            foreach (var item in layoutItems.OrderBy(entry => entry.TopY))
            {
                builder.AppendLine(
                    $"<!-- block: page={pageNumber} kind={item.KindLabel} confidence={item.Confidence:0.###} -->");

                if (item.ItemKind == LayoutItemKind.Table)
                {
                    builder.AppendLine();
                    builder.AppendLine("### Table");
                    builder.AppendLine();
                }

                builder.AppendLine(item.Markdown);
                builder.AppendLine();
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string FormatParagraph(DocumentParagraph paragraph)
    {
        if (paragraph.Role == ParagraphRole.SectionHeading ||
            paragraph.Role == ParagraphRole.Title)
        {
            return $"### {paragraph.Content.Trim()}";
        }

        return paragraph.Content.Trim();
    }

    private static float GetPageConfidence(DocumentPage? page)
    {
        if (page is null || page.Words.Count == 0)
        {
            return 1.0f;
        }

        return (float)page.Words.Average(word => word.Confidence);
    }

    private static bool TryGetRegionOnPage(
        IReadOnlyList<BoundingRegion>? regions,
        int pageNumber,
        out BoundingRegion region)
    {
        region = default;

        if (regions is null || regions.Count == 0)
        {
            return false;
        }

        region = regions[0];
        return region.PageNumber == pageNumber;
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

    private sealed record LayoutItem(
        float TopY,
        LayoutItemKind ItemKind,
        string Markdown,
        string KindLabel,
        float Confidence);

    private enum LayoutItemKind
    {
        Paragraph,
        Table
    }
}
