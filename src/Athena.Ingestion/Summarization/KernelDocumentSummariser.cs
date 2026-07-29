using System.Text.Json;
using Athena.Core.Records;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SkKernel = Microsoft.SemanticKernel.Kernel;

namespace Athena.Ingestion.Summarization;

public sealed class KernelDocumentSummariser : IDocumentSummariser
{
    private const int MaxInputCharacters = 24_000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly SkKernel _kernel;
    private readonly ILogger<KernelDocumentSummariser> _logger;

    public KernelDocumentSummariser(
        SkKernel kernel,
        ILogger<KernelDocumentSummariser> logger)
    {
        _kernel = kernel;
        _logger = logger;
    }

    public async Task<DocumentSummaryResult> SummarizeAsync(
        DocumentMetadata metadata,
        string documentText,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentText);

        var chat = _kernel.GetRequiredService<IChatCompletionService>();
        var input = Truncate(documentText, MaxInputCharacters);

        var prompt = $$"""
            You are indexing research PDFs for a financial research librarian.
            Summarize the document below and extract topic tags.

            Document title: {{metadata.Title}}
            Cluster: {{metadata.Cluster}}

            Return ONLY valid JSON with this shape:
            {
              "summary": "concise summary in at most {{DocRecord.MaxSummaryWords}} words",
              "topics": ["tag1", "tag2", "tag3"]
            }

            Rules:
            - topics must contain between {{DocRecord.MinTopicCount}} and {{DocRecord.MaxTopicCount}} short phrases
            - summary must be factual and based only on the supplied text
            - do not include markdown fences

            Document text:
            {{input}}
            """;

        var response = await chat.GetChatMessageContentAsync(
            prompt,
            new OpenAIPromptExecutionSettings
            {
                Temperature = 0.1,
                MaxTokens = 600,
                ResponseFormat = "json_object"
            },
            _kernel,
            ct).ConfigureAwait(false);

        var payload = response.Content ?? string.Empty;
        var parsed = JsonSerializer.Deserialize<SummariserResponse>(payload, JsonOptions)
                     ?? throw new InvalidOperationException("Document summariser returned empty JSON.");

        var summary = TrimToWordLimit(parsed.Summary, DocRecord.MaxSummaryWords);
        var topics = NormalizeTopics(parsed.Topics);

        _logger.LogInformation(
            "Summarised {DocId}: {TopicCount} topics, {SummaryWords} words.",
            metadata.DocId,
            topics.Count,
            CountWords(summary));

        return new DocumentSummaryResult
        {
            Summary = summary,
            Topics = topics
        };
    }

    private static string Truncate(string text, int maxCharacters) =>
        text.Length <= maxCharacters ? text : text[..maxCharacters];

    private static string TrimToWordLimit(string summary, int maxWords)
    {
        var words = summary.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Length <= maxWords
            ? summary.Trim()
            : string.Join(' ', words.Take(maxWords));
    }

    private static IReadOnlyList<string> NormalizeTopics(IReadOnlyList<string>? topics)
    {
        var normalized = (topics ?? [])
            .Select(topic => topic.Trim())
            .Where(topic => !string.IsNullOrWhiteSpace(topic))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(DocRecord.MaxTopicCount)
            .ToList();

        if (normalized.Count < DocRecord.MinTopicCount)
        {
            throw new InvalidOperationException(
                $"Document summariser returned {normalized.Count} topics; expected at least {DocRecord.MinTopicCount}.");
        }

        return normalized;
    }

    private static int CountWords(string text) =>
        text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;

    private sealed class SummariserResponse
    {
        public string Summary { get; set; } = string.Empty;

        public List<string> Topics { get; set; } = [];
    }
}
