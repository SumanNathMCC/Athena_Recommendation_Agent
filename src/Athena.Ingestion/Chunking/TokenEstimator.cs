namespace Athena.Ingestion.Chunking;

public static class TokenEstimator
{
    private const double CharsPerToken = 4.0;

    public static int Estimate(string? text) =>
        string.IsNullOrWhiteSpace(text) ? 0 : (int)Math.Ceiling(text.Length / CharsPerToken);

    public static IEnumerable<string> SplitWindows(
        string text,
        int maxTokens,
        double overlapRatio)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxTokens, 0);
        ArgumentOutOfRangeException.ThrowIfNegative(overlapRatio);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(overlapRatio, 0.95);

        var maxChars = (int)Math.Ceiling(maxTokens * CharsPerToken);
        var overlapChars = (int)Math.Ceiling(maxTokens * overlapRatio * CharsPerToken);
        var stepChars = Math.Max(1, maxChars - overlapChars);

        if (text.Length <= maxChars)
        {
            yield return text.Trim();
            yield break;
        }

        var start = 0;
        while (start < text.Length)
        {
            var end = Math.Min(start + maxChars, text.Length);
            if (end < text.Length)
            {
                var breakAt = text.LastIndexOf(' ', end - 1, Math.Min(end - start, 200));
                if (breakAt > start + (maxChars / 2))
                {
                    end = breakAt;
                }
            }

            var window = text[start..end].Trim();
            if (!string.IsNullOrWhiteSpace(window))
            {
                yield return window;
            }

            if (end >= text.Length)
            {
                yield break;
            }

            start += stepChars;
        }
    }
}
