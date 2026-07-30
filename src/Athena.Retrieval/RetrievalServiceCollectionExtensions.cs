using Athena.Core.Indexing;
using Microsoft.Extensions.DependencyInjection;

namespace Athena.Retrieval;

public static class RetrievalServiceCollectionExtensions
{
    public static IServiceCollection AddAthenaRetrieval(this IServiceCollection services)
    {
        services.AddSingleton<LuceneChunkIndex>();
        services.AddSingleton<IChunkIndexSync>(sp => sp.GetRequiredService<LuceneChunkIndex>());
        services.AddSingleton<IDenseRetriever, DenseChunkRetriever>();
        services.AddSingleton<ILexicalRetriever, LuceneLexicalRetriever>();
        services.AddSingleton<IReranker, LlmReranker>();
        services.AddSingleton<IHybridRetriever, HybridRetriever>();
        services.AddScoped<IRetrievedContextAccessor, RetrievedContextAccessor>();
        return services;
    }
}
