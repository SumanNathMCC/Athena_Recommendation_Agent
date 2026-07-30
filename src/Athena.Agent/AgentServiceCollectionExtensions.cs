using Athena.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using SkKernel = Microsoft.SemanticKernel.Kernel;

namespace Athena.Agent;

public static class AgentServiceCollectionExtensions
{
    public static IServiceCollection AddAthenaAgent(this IServiceCollection services)
    {
        services.AddSingleton<SearchPlugin>();
        services.AddSingleton<ChatCompletionAgent>(sp =>
        {
            var kernel = sp.GetRequiredService<SkKernel>();
            var searchPlugin = sp.GetRequiredService<SearchPlugin>();
            if (!kernel.Plugins.Contains("Search"))
            {
                kernel.Plugins.AddFromObject(searchPlugin, pluginName: "Search");
            }

            return AthenaAgentFactory.Create(kernel);
        });

        return services;
    }
}
