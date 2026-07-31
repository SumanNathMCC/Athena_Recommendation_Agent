using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Athena.Recommendation;

public static class RecommendationServiceCollectionExtensions
{
    public static IServiceCollection AddAthenaRecommendation(this IServiceCollection services)
    {
        services.AddOptions<RecommendationOptions>();

        services.AddSingleton<IDiversifier, MmrDiversifier>();
        services.AddSingleton<INearDuplicateResolver>(sp =>
        {
            var options = sp.GetService<IOptions<RecommendationOptions>>()?.Value
                          ?? new RecommendationOptions();
            return new LineageNearDuplicateResolver(options.NearDuplicateCeiling);
        });
        services.AddSingleton<IRecommendationScorer>(sp =>
            new BlendedRecommendationScorer(
                sp.GetRequiredService<IOptions<RecommendationOptions>>().Value));
        services.AddSingleton<IDocumentCatalog, VectorStoreDocumentCatalog>();

        // Scoped: one interest profile / surfaced set per Blazor circuit (session).
        services.AddScoped<IInterestProfileStore, SessionInterestProfileStore>();
        services.AddScoped<ISessionContext, CircuitSessionContext>();
        services.AddScoped<IDocumentRecommender>(sp =>
            new DocumentRecommender(
                sp.GetRequiredService<IDocumentCatalog>(),
                sp.GetRequiredService<IDiversifier>(),
                sp.GetRequiredService<INearDuplicateResolver>(),
                sp.GetRequiredService<IRecommendationScorer>(),
                sp.GetRequiredService<IInterestProfileStore>(),
                sp.GetRequiredService<Athena.Retrieval.IHybridRetriever>(),
                sp.GetRequiredService<Athena.Ingestion.Embeddings.ICorpusEmbeddingService>(),
                sp.GetRequiredService<IOptions<RecommendationOptions>>()));

        return services;
    }
}

/// <summary>
/// Per-circuit session id for interest-profile scoping.
/// </summary>
public interface ISessionContext
{
    string SessionId { get; }
}

public sealed class CircuitSessionContext : ISessionContext
{
    public string SessionId { get; } = Guid.NewGuid().ToString("N");
}
