using System.Text.RegularExpressions;
using Markdig;

namespace Athena.Web.Rendering;

/// <summary>
/// Converts assistant/passage text to HTML (markdown tables, etc.) and preserves math for KaTeX.
/// </summary>
public static partial class MarkdownMathRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UsePipeTables()
        .DisableHtml()
        .Build();

    public static string ToHtml(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var placeholders = new List<string>();
        var protectedText = ProtectMath(NormalizeMathDelimiters(text), placeholders);
        var html = Markdown.ToHtml(protectedText, Pipeline);
        return RestoreMath(html, placeholders);
    }

    /// <summary>
    /// Normalize common LaTeX wrappers so KaTeX auto-render can find them after markdown conversion.
    /// </summary>
    internal static string NormalizeMathDelimiters(string text)
    {
        text = DisplayBracketRegex().Replace(text, static m => $"$$\n{m.Groups[1].Value.Trim()}\n$$");
        text = InlineParenRegex().Replace(text, static m => $"${m.Groups[1].Value.Trim()}$");
        return text;
    }

    private static string ProtectMath(string text, List<string> placeholders)
    {
        text = DisplayDollarRegex().Replace(text, m => Store(placeholders, m.Value));
        text = InlineDollarRegex().Replace(text, m => Store(placeholders, m.Value));
        return text;
    }

    private static string RestoreMath(string html, List<string> placeholders)
    {
        for (var i = 0; i < placeholders.Count; i++)
        {
            // HtmlEncode so accidental < in TeX cannot break markup; browser textContent is decoded for KaTeX.
            html = html.Replace(Token(i), System.Net.WebUtility.HtmlEncode(placeholders[i]), StringComparison.Ordinal);
        }

        return html;
    }

    private static string Store(List<string> placeholders, string value)
    {
        placeholders.Add(value);
        return Token(placeholders.Count - 1);
    }

    private static string Token(int index) => $"MATHPLACEHOLDER{index}X";

    [GeneratedRegex(@"\\\[([\s\S]*?)\\\]", RegexOptions.CultureInvariant)]
    private static partial Regex DisplayBracketRegex();

    [GeneratedRegex(@"\\\(([\s\S]*?)\\\)", RegexOptions.CultureInvariant)]
    private static partial Regex InlineParenRegex();

    [GeneratedRegex(@"\$\$([\s\S]*?)\$\$", RegexOptions.CultureInvariant)]
    private static partial Regex DisplayDollarRegex();

    [GeneratedRegex(@"(?<!\$)\$(?!\$)([^\$\n]+?)\$(?!\$)", RegexOptions.CultureInvariant)]
    private static partial Regex InlineDollarRegex();
}
