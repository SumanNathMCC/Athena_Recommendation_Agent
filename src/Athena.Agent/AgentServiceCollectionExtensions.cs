using Athena.Filters.Grounding;
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
        // Scoped so SearchPlugin shares the circuit's IRetrievedContextAccessor with the chat UI.
        services.AddScoped<SearchPlugin>();
        services.AddScoped<ChatCompletionAgent>(sp =>
        {
            var rootKernel = sp.GetRequiredService<SkKernel>();
            var searchPlugin = sp.GetRequiredService<SearchPlugin>();

            // Fresh kernel per circuit so plugin instances are not shared across users.
            var agentKernel = new SkKernel(rootKernel.Services);
            agentKernel.Plugins.AddFromObject(searchPlugin, pluginName: "Search");
            agentKernel.FunctionInvocationFilters.Add(sp.GetRequiredService<GroundingGuardFilter>());

            return AthenaAgentFactory.Create(agentKernel);
        });

        return services;
    }
}
