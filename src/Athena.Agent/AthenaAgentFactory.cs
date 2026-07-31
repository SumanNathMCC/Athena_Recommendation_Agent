using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SkKernel = Microsoft.SemanticKernel.Kernel;

namespace Athena.Agent;

public static class AthenaAgentFactory
{
    public const string DefaultPromptFile = "Agents/AthenaResearchLibrarian.md";

    public static ChatCompletionAgent Create(
        SkKernel kernel,
        string contentRootPath,
        string? promptFile = null)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);

        var agentPrompt = AgentPromptLoader.Load(
            contentRootPath,
            string.IsNullOrWhiteSpace(promptFile) ? DefaultPromptFile : promptFile);

        var functionChoice = FunctionChoiceBehavior.Auto();

        return new ChatCompletionAgent
        {
            Name = agentPrompt.Name,
            Instructions = agentPrompt.Instructions,
            Kernel = kernel,
            Arguments = new KernelArguments(new OpenAIPromptExecutionSettings
            {
                Temperature = agentPrompt.Temperature,
                FunctionChoiceBehavior = functionChoice
            })
        };
    }
}
