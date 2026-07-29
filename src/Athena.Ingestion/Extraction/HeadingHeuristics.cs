using System.Text.RegularExpressions;

namespace Athena.Ingestion.Extraction;

internal static class HeadingHeuristics
{
    private static readonly Regex PrincipleHeading = new(
        @"^Principle\s+\d+\s*:",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex RomanNumeralHeading = new(
        @"^[IVXLC]+\.\s+\S",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex NumberedHeading = new(
        @"^\d+(?:\.\d+)*\s+[A-Za-z]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> KnownSectionTitles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Abstract",
        "Contents",
        "Introduction",
        "Conclusion",
        "References",
        "Acknowledgements",
        "Acknowledgments",
        "Appendix",
        "Summary",
        "Governance",
        "Operational risk management",
        "Business continuity planning and testing",
        "Mapping interconnections and interdependencies",
        "Third-party dependency management",
        "Incident management",
        "ICT including cyber security"
    };

    public static bool IsLikelyHeading(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        var trimmed = content.Trim();
        if (trimmed.Length > 200)
        {
            return false;
        }

        if (PrincipleHeading.IsMatch(trimmed) ||
            RomanNumeralHeading.IsMatch(trimmed) ||
            NumberedHeading.IsMatch(trimmed))
        {
            return true;
        }

        if (KnownSectionTitles.Contains(trimmed))
        {
            return true;
        }

        return trimmed.Length <= 80 &&
               trimmed.Equals(trimmed.ToUpperInvariant(), StringComparison.Ordinal) &&
               trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2;
    }
}
