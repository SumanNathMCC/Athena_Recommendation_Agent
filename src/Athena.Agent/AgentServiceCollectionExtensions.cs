using Athena.Filters.Grounding;
using Athena.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
            var configuration = sp.GetRequiredService<IConfiguration>();
            var environment = sp.GetService<IHostEnvironment>();

            // Fresh kernel per circuit so plugin instances are not shared across users.
            var agentKernel = new SkKernel(rootKernel.Services);
            agentKernel.Plugins.AddFromObject(searchPlugin, pluginName: "Search");
            agentKernel.FunctionInvocationFilters.Add(sp.GetRequiredService<GroundingGuardFilter>());

            var promptFile = configuration["Agent:PromptFile"] ?? AthenaAgentFactory.DefaultPromptFile;
            var contentRoot = environment?.ContentRootPath ?? AppContext.BaseDirectory;
            return AthenaAgentFactory.Create(agentKernel, contentRoot, promptFile);
        });

        return services;
    }
}
