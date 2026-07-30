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
    private readonly ChatHistoryAgentThread _thread = new();

    public AthenaChatSession(ChatCompletionAgent agent)
    {
        _agent = agent;
    }

    public async Task<string> SendAsync(string userMessage, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return string.Empty;
        }

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
        return string.IsNullOrWhiteSpace(text)
            ? "I could not produce a reply. Ensure the corpus is injected and Azure Foundry is configured."
            : text;
    }
}
