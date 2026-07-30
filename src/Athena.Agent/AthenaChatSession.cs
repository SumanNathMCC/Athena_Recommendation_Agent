using Athena.Retrieval;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Athena.Agent;

/// <summary>
/// Per-circuit chat session backed by ChatCompletionAgent + hybrid SearchPlugin.
/// </summary>
public sealed class AthenaChatSession
{
    private readonly ChatCompletionAgent _agent;
    private readonly IRetrievedContextAccessor _retrievedContext;
    private readonly ChatHistoryAgentThread _thread = new();

    public AthenaChatSession(
        ChatCompletionAgent agent,
        IRetrievedContextAccessor retrievedContext)
    {
        _agent = agent;
        _retrievedContext = retrievedContext;
    }

    public async Task<(string Reply, IReadOnlyList<Passage> Passages)> SendAsync(
        string userMessage,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return (string.Empty, Array.Empty<Passage>());
        }

        _retrievedContext.Clear();

        var sb = new System.Text.StringBuilder();
        await foreach (var item in _agent.InvokeAsync(
                           message: new ChatMessageContent(AuthorRole.User, userMessage.Trim()),
                           thread: _thread,
                           options: null,
                           cancellationToken: ct).ConfigureAwait(false))
        {
            if (!string.IsNullOrEmpty(item.Message.Content))
            {
                sb.Append(item.Message.Content);
            }
        }

        var text = sb.ToString().Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            text = "I could not produce a reply. Ensure the corpus is injected and Azure Foundry is configured.";
        }

        IReadOnlyList<Passage> passages = _retrievedContext.LastPassages.ToList();
        return (text, passages);
    }
}
