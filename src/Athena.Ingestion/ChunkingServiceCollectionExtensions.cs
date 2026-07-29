using Athena.Ingestion.Chunking;
using Microsoft.Extensions.DependencyInjection;

namespace Athena.Ingestion;

public static class ChunkingServiceCollectionExtensions
{
    public static IServiceCollection AddAthenaChunking(this IServiceCollection services)
    {
        services.AddSingleton<FixedWindowChunker>();
        services.AddSingleton<SectionAwareChunker>();
        services.AddSingleton<IChunkerFactory, ChunkerFactory>();

        services.AddSingleton<IChunker>(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ChunkingOptions>>().Value;
            return sp.GetRequiredService<IChunkerFactory>().GetChunker(options.Strategy);
        });

        return services;
    }
}
