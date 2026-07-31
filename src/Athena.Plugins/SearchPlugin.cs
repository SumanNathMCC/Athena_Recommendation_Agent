using System.Text;
using System.ComponentModel;
using Athena.Plugins.Prompts;
using Athena.Retrieval;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SkKernel = Microsoft.SemanticKernel.Kernel;

namespace Athena.Plugins;

/// <summary>
/// Thin Semantic Kernel plugin over hybrid retrieval (Part B) and grounded answering (Part C).
/// </summary>
public sealed class SearchPlugin
{
    public const string InsufficientContext = "INSUFFICIENT_CONTEXT";

    private readonly IHybridRetriever _hybridRetriever;
    private readonly IRetrievedContextAccessor _retrievedContext;
    private readonly SkKernel _kernel;

    public SearchPlugin(
        IHybridRetriever hybridRetriever,
        IRetrievedContextAccessor retrievedContext,
        SkKernel kernel)
    {
        _hybridRetriever = hybridRetriever;
        _retrievedContext = retrievedContext;
        _kernel = kernel;
    }

    [KernelFunction("hybrid_search")]
    [Description(
        "Search the research corpus for relevant passages using hybrid dense+lexical retrieval. " +
        "Use when the user wants matching excerpts, evidence, or sources without a full answer. " +
        "Optional docId restricts search to one document (e.g. A1, B2).")]
    public async Task<string> HybridSearchAsync(
        [Description("The search query")] string query,
        [Description("Optional document id filter such as A1 or C3")] string? docId = null,
        [Description("Number of passages to return (default 6)")] int topK = 6,
        CancellationToken cancellationToken = default)
    {
        var passages = await _hybridRetriever
            .RetrieveAsync(query, topK <= 0 ? 6 : topK, NormalizeDocId(docId), cancellationToken)
            .ConfigureAwait(false);

        _retrievedContext.Set(passages, query);

        if (passages.Count == 0)
        {
            return "No matching passages found in the indexed corpus.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Found {passages.Count} passage(s):");
        for (var i = 0; i < passages.Count; i++)
        {
            var p = passages[i];
            var snippet = p.Text;
            sb.AppendLine($"{i + 1}. [{p.Title}, p.{p.PageNumber}] (doc={p.DocId}, score={p.Score:F3})");
            sb.AppendLine(snippet);
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    [KernelFunction("answer_question")]
    [Description(
        "Answer a factual question using retrieved passages from the research corpus. " +
        "Call this for questions about papers, principles, methods, or document contents. " +
        "Every factual claim must be cited as [Title, p.N]. " +
        "Do not use for open-ended reading recommendations.")]
    public async Task<string> AnswerQuestionAsync(
        [Description("The user's question")] string question,
        [Description("Optional document id filter such as A1")] string? docId = null,
        CancellationToken cancellationToken = default)
    {
        var passages = await _hybridRetriever
            .RetrieveAsync(question, HybridRetriever.DefaultTopK, NormalizeDocId(docId), cancellationToken)
            .ConfigureAwait(false);

        _retrievedContext.Set(passages, question);

        if (passages.Count == 0)
        {
            return InsufficientContext;
        }

        var context = FormatContext(passages);
        var prompt = AnswerPromptLoader.Render(question, context);
        var chat = _kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();
        history.AddUserMessage(prompt);

        var response = await chat.GetChatMessageContentAsync(history, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var answer = response.Content?.Trim();
        return string.IsNullOrWhiteSpace(answer) ? InsufficientContext : answer;
    }

    private static string? NormalizeDocId(string? docId) =>
        string.IsNullOrWhiteSpace(docId) ? null : docId.Trim();

    private static string FormatContext(IReadOnlyList<Passage> passages)
    {
        var sb = new StringBuilder();
        foreach (var p in passages)
        {
            sb.AppendLine($"--- [{p.Title}, p.{p.PageNumber}] docId={p.DocId} chunkId={p.ChunkId}");
            sb.AppendLine(p.Text);
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
