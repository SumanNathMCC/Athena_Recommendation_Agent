using System.Text;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SkKernel = Microsoft.SemanticKernel.Kernel;

namespace Athena.Retrieval;

/// <summary>
/// LLM reranker that scores each candidate passage 0–10 for the query.
/// </summary>
public sealed class LlmReranker : IReranker
{
    private readonly SkKernel _kernel;

    public LlmReranker(SkKernel kernel)
    {
        _kernel = kernel;
    }

    public async Task<IReadOnlyList<Passage>> RerankAsync(
        string query,
        IReadOnlyList<Passage> candidates,
        int topK,
        CancellationToken ct = default)
    {
        if (candidates.Count == 0 || topK <= 0)
        {
            return Array.Empty<Passage>();
        }

        if (candidates.Count <= topK)
        {
            // Still score so Score reflects relevance, but keep order if LLM fails.
        }

        try
        {
            var chat = _kernel.GetRequiredService<IChatCompletionService>();
            var prompt = BuildPrompt(query, candidates);
            var history = new ChatHistory();
            history.AddSystemMessage(
                "You are a relevance judge. Reply with ONLY a JSON array of objects " +
                "{\"chunkId\":\"...\",\"score\":N} where N is an integer 0-10. " +
                "Score every candidate. No markdown.");
            history.AddUserMessage(prompt);

            var response = await chat.GetChatMessageContentAsync(history, cancellationToken: ct)
                .ConfigureAwait(false);
            var text = response.Content ?? string.Empty;
            var scores = ParseScores(text);
            if (scores.Count == 0)
            {
                return candidates.Take(topK).ToList();
            }

            return candidates
                .Select(p => p with { Score = scores.TryGetValue(p.ChunkId, out var s) ? s : 0 })
                .OrderByDescending(p => p.Score)
                .ThenBy(p => p.ChunkId, StringComparer.Ordinal)
                .Take(topK)
                .ToList();
        }
        catch
        {
            return candidates.Take(topK).ToList();
        }
    }

    private static string BuildPrompt(string query, IReadOnlyList<Passage> candidates)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Query: {query}");
        sb.AppendLine();
        sb.AppendLine("Candidates:");
        for (var i = 0; i < candidates.Count; i++)
        {
            var c = candidates[i];
            var snippet = c.Text.Length > 600 ? c.Text[..600] + "…" : c.Text;
            sb.AppendLine($"[{i + 1}] chunkId={c.ChunkId} title={c.Title} page={c.PageNumber}");
            sb.AppendLine(snippet);
            sb.AppendLine();
        }

        sb.AppendLine("Return JSON array scoring every chunkId from 0 (irrelevant) to 10 (highly relevant).");
        return sb.ToString();
    }

    private static Dictionary<string, double> ParseScores(string text)
    {
        var json = ExtractJsonArray(text);
        var result = new Dictionary<string, double>(StringComparer.Ordinal);
        if (json is null)
        {
            return result;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty("chunkId", out var idProp))
                {
                    continue;
                }

                var id = idProp.GetString();
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                double score = 0;
                if (item.TryGetProperty("score", out var scoreProp))
                {
                    score = scoreProp.ValueKind switch
                    {
                        JsonValueKind.Number => scoreProp.GetDouble(),
                        JsonValueKind.String when double.TryParse(scoreProp.GetString(), out var parsed) => parsed,
                        _ => 0
                    };
                }

                result[id] = Math.Clamp(score, 0, 10);
            }
        }
        catch (JsonException)
        {
            // fall through empty
        }

        return result;
    }

    private static string? ExtractJsonArray(string text)
    {
        var start = text.IndexOf('[');
        var end = text.LastIndexOf(']');
        if (start < 0 || end <= start)
        {
            return null;
        }

        return text[start..(end + 1)];
    }
}
