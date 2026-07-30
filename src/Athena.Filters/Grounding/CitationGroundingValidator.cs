using System.Text.RegularExpressions;
using Athena.Retrieval;

namespace Athena.Filters.Grounding;

public sealed record Citation(string Title, int PageNumber, string Raw);

public sealed record GroundingViolation(
    string Code,
    string Message,
    string? Citation = null,
    string? Sentence = null);

public sealed record GroundingCheckResult(
    bool IsValid,
    IReadOnlyList<Citation> Citations,
    IReadOnlyList<GroundingViolation> Violations)
{
    public static GroundingCheckResult Ok(IReadOnlyList<Citation> citations) =>
        new(true, citations, Array.Empty<GroundingViolation>());

    public static GroundingCheckResult Fail(
        IReadOnlyList<Citation> citations,
        IReadOnlyList<GroundingViolation> violations) =>
        new(false, citations, violations);
}

/// <summary>
/// Validates [Title, p.N] citations against retrieved passages (Part C).
/// </summary>
public static partial class CitationGroundingValidator
{
    public const string InsufficientContext = "INSUFFICIENT_CONTEXT";

    [GeneratedRegex(@"\[(?<title>[^\[\]]+?),\s*p\.(?<page>\d+)\]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CitationRegex();

    public static IReadOnlyList<Citation> ParseCitations(string answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return Array.Empty<Citation>();
        }

        var list = new List<Citation>();
        foreach (Match match in CitationRegex().Matches(answer))
        {
            if (!int.TryParse(match.Groups["page"].Value, out var page))
            {
                continue;
            }

            list.Add(new Citation(
                match.Groups["title"].Value.Trim(),
                page,
                match.Value));
        }

        return list;
    }

    public static GroundingCheckResult Validate(string answer, IReadOnlyList<Passage> passages)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return GroundingCheckResult.Fail(
                Array.Empty<Citation>(),
                [new GroundingViolation("empty_answer", "Answer was empty.")]);
        }

        var trimmed = answer.Trim();
        if (string.Equals(trimmed, InsufficientContext, StringComparison.Ordinal))
        {
            return GroundingCheckResult.Ok(Array.Empty<Citation>());
        }

        var citations = ParseCitations(trimmed);
        var violations = new List<GroundingViolation>();

        foreach (var citation in citations)
        {
            if (!CitationMatchesPassage(citation, passages))
            {
                violations.Add(new GroundingViolation(
                    "unknown_citation",
                    "Citation does not match any retrieved passage.",
                    citation.Raw));
            }
        }

        foreach (var sentence in SplitSentences(trimmed))
        {
            if (!LooksFactual(sentence))
            {
                continue;
            }

            if (!CitationRegex().IsMatch(sentence))
            {
                violations.Add(new GroundingViolation(
                    "missing_citation",
                    "Factual sentence is missing a [Title, p.N] citation.",
                    Sentence: sentence));
            }
        }

        return violations.Count == 0
            ? GroundingCheckResult.Ok(citations)
            : GroundingCheckResult.Fail(citations, violations);
    }

    internal static bool CitationMatchesPassage(Citation citation, IReadOnlyList<Passage> passages)
    {
        foreach (var passage in passages)
        {
            if (passage.PageNumber != citation.PageNumber)
            {
                continue;
            }

            if (TitlesMatch(citation.Title, passage.Title))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool TitlesMatch(string cited, string passageTitle)
    {
        var a = NormalizeTitle(cited);
        var b = NormalizeTitle(passageTitle);
        if (a.Length == 0 || b.Length == 0)
        {
            return false;
        }

        return a == b || a.Contains(b, StringComparison.Ordinal) || b.Contains(a, StringComparison.Ordinal);
    }

    private static string NormalizeTitle(string title) =>
        string.Join(' ', title.Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    internal static IReadOnlyList<string> SplitSentences(string text)
    {
        var parts = Regex.Split(text.Trim(), @"(?<=[\.!\?])\s+")
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();

        // Attach a trailing citation-only fragment to the previous sentence.
        var merged = new List<string>();
        foreach (var part in parts)
        {
            var withoutCitations = CitationRegex().Replace(part, string.Empty).Trim();
            if (merged.Count > 0 && withoutCitations.Length == 0 && CitationRegex().IsMatch(part))
            {
                merged[^1] = $"{merged[^1]} {part}".Trim();
                continue;
            }

            merged.Add(part);
        }

        return merged;
    }

    private static bool LooksFactual(string sentence)
    {
        var withoutCitations = CitationRegex().Replace(sentence, string.Empty).Trim();
        if (withoutCitations.Length < 12)
        {
            return false;
        }

        // Skip pure meta / refusal lines.
        if (withoutCitations.Contains(InsufficientContext, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return withoutCitations.Any(char.IsLetter);
    }
}
