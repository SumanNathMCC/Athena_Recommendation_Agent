namespace Athena.Recommendation;

/// <summary>
/// Cosine similarity helpers for document / profile vectors.
/// </summary>
public static class VectorMath
{
    public static double CosineSimilarity(ReadOnlyMemory<float> a, ReadOnlyMemory<float> b)
    {
        var left = a.Span;
        var right = b.Span;
        if (left.Length == 0 || right.Length == 0 || left.Length != right.Length)
        {
            return 0d;
        }

        double dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < left.Length; i++)
        {
            dot += left[i] * right[i];
            normA += left[i] * left[i];
            normB += right[i] * right[i];
        }

        if (normA <= 0 || normB <= 0)
        {
            return 0d;
        }

        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }

    public static ReadOnlyMemory<float> Blend(
        ReadOnlyMemory<float>? existing,
        ReadOnlyMemory<float> incoming,
        double decay)
    {
        if (existing is null || existing.Value.Length == 0)
        {
            return incoming;
        }

        var prev = existing.Value.Span;
        var next = incoming.Span;
        if (prev.Length != next.Length)
        {
            return incoming;
        }

        var blended = new float[prev.Length];
        var keep = Math.Clamp(decay, 0d, 1d);
        var mix = 1d - keep;
        for (var i = 0; i < blended.Length; i++)
        {
            blended[i] = (float)(keep * prev[i] + mix * next[i]);
        }

        return blended;
    }
}
