using Athena.Filters.Grounding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

namespace Athena.Filters;

public static class FiltersServiceCollectionExtensions
{
    public static IServiceCollection AddAthenaFilters(
        this IServiceCollection services,
        string? citationViolationLogPath = null)
    {
        if (!string.IsNullOrWhiteSpace(citationViolationLogPath))
        {
            services.Configure<CitationViolationLogOptions>(options =>
                options.LogFilePath = citationViolationLogPath);
        }
        else
        {
            services.Configure<CitationViolationLogOptions>(_ => { });
        }

        services.AddSingleton<ICitationViolationLogger, CitationViolationLogger>();
        // Registered as concrete type only; Agent attaches it to the per-circuit kernel explicitly
        // to avoid double-invocation via Kernel DI auto-discovery.
        services.AddScoped<GroundingGuardFilter>();
        return services;
    }
}
