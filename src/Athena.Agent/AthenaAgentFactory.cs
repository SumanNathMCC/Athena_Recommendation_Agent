using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SkKernel = Microsoft.SemanticKernel.Kernel;

namespace Athena.Agent;

public static class AthenaAgentFactory
{
    public const string AgentName = "Athena";

    public static ChatCompletionAgent Create(SkKernel kernel)
    {
        ArgumentNullException.ThrowIfNull(kernel);

        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = 0.1,
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        return new ChatCompletionAgent
        {
            Name = AgentName,
            Instructions =
                """
                You are Athena, a research librarian for a curated document corpus.
                For factual questions about the corpus, call answer_question.
                For listing matching passages or evidence, call hybrid_search.
                If the user asks something unrelated to the research library (sports, trivia, personal advice),
                politely refuse without calling tools.
                Prefer concise, grounded replies. Do not invent citations.
                Recommendation tools are not available yet — if asked only for reading suggestions,
                say recommendations are coming soon and offer to answer a factual question instead.
                """,
            Kernel = kernel,
            Arguments = new KernelArguments(settings)
        };
    }
}
