namespace Athena.Recommendation;

/// <summary>
/// Tunables for blended recommendation scoring.
/// </summary>
public sealed class RecommendationOptions
{
    public const string SectionName = "Recommendation";

    /// <summary>Weight for document-vector cosine to the query/seed.</summary>
    public double DocSimWeight { get; set; } = 0.50;

    /// <summary>Weight for normalised chunk-hit aggregation.</summary>
    public double ChunkHitWeight { get; set; } = 0.35;

    /// <summary>Weight for recency prior.</summary>
    public double RecencyWeight { get; set; } = 0.15;

    /// <summary>
    /// Recency half-life style tau in days: score = exp(-ageDays / tau).
    /// Two years keeps recent arXiv/regulatory updates ahead without erasing classics.
    /// </summary>
    public double RecencyTauDays { get; set; } = 730d;

    /// <summary>Cosine ceiling used by the near-duplicate resolver.</summary>
    public double NearDuplicateCeiling { get; set; } = LineageNearDuplicateResolver.DefaultSimilarityCeiling;

    /// <summary>Default MMR lambda.</summary>
    public double DefaultLambda { get; set; } = 0.7;

    /// <summary>How many chunk hits to pull for recommend_for_query aggregation.</summary>
    public int ChunkHitTopK { get; set; } = 20;
}
