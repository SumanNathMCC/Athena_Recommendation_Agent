using System.Text;
using System.ComponentModel;
using Athena.Retrieval;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SkKernel = Microsoft.SemanticKernel.Kernel;

namespace Athena.Plugins;

/// <summary>
/// Thin Semantic Kernel plugin over hybrid retrieval (Part B) and light Q&amp;A (Part C later).
/// </summary>
public sealed class SearchPlugin
{
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
            var snippet = p.Text.Length > 400 ? p.Text[..400] + "…" : p.Text;
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
            return "INSUFFICIENT_CONTEXT";
        }

        var context = FormatContext(passages);
        var chat = _kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();
        history.AddSystemMessage(
            """
            You are Athena, a research librarian. Answer ONLY using the provided CONTEXT passages.
            Prefer citing sources as [Title, p.N] after factual claims.
            If the context does not support an answer, reply exactly: INSUFFICIENT_CONTEXT
            Do not invent facts outside the context.
            """);
        history.AddUserMessage($"CONTEXT:\n{context}\n\nQUESTION:\n{question}");

        var response = await chat.GetChatMessageContentAsync(history, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var answer = response.Content?.Trim();
        return string.IsNullOrWhiteSpace(answer) ? "INSUFFICIENT_CONTEXT" : answer;
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
