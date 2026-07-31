using System.Runtime.CompilerServices;
using System.Text;
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

    /// <summary>
    /// Streams assistant text deltas from the model (SK agent streaming API).
    /// Passages are available via <see cref="GetLastPassages"/> after tool calls complete.
    /// </summary>
    public async IAsyncEnumerable<string> StreamAsync(
        string userMessage,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            yield break;
        }

        _retrievedContext.Clear();

        await foreach (var item in _agent.InvokeStreamingAsync(
                           message: new ChatMessageContent(AuthorRole.User, userMessage.Trim()),
                           thread: _thread,
                           options: null,
                           cancellationToken: ct).ConfigureAwait(false))
        {
            var message = item.Message;
            if (message is null ||
                message.Role == AuthorRole.Tool ||
                message.Role == AuthorRole.User ||
                message.Role == AuthorRole.System)
            {
                continue;
            }

            var delta = message.Content;
            if (string.IsNullOrEmpty(delta))
            {
                continue;
            }

            yield return delta;
        }
    }

    public IReadOnlyList<Passage> GetLastPassages() =>
        _retrievedContext.LastPassages.ToList();

    public async Task<(string Reply, IReadOnlyList<Passage> Passages)> SendAsync(
        string userMessage,
        CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        await foreach (var delta in StreamAsync(userMessage, ct).ConfigureAwait(false))
        {
            sb.Append(delta);
        }

        var text = sb.ToString().Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            text = "I could not produce a reply. Ensure the corpus is injected and Azure Foundry is configured.";
        }

        return (text, GetLastPassages());
    }
}
