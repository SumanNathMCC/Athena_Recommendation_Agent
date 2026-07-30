using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Athena.Filters.Grounding;

public sealed class CitationViolationLogOptions
{
    public const string SectionName = "CitationViolationLog";

    /// <summary>Absolute or relative path to the JSONL log file.</summary>
    public string LogFilePath { get; set; } = Path.Combine("logs", "citation-violations.jsonl");
}

public interface ICitationViolationLogger
{
    Task LogAsync(
        string question,
        string answer,
        IReadOnlyList<GroundingViolation> violations,
        CancellationToken ct = default);
}

public sealed class CitationViolationLogger : ICitationViolationLogger
{
    private readonly string _logFilePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public CitationViolationLogger(IOptions<CitationViolationLogOptions> options)
    {
        _logFilePath = options.Value.LogFilePath;
    }

    public async Task LogAsync(
        string question,
        string answer,
        IReadOnlyList<GroundingViolation> violations,
        CancellationToken ct = default)
    {
        var directory = Path.GetDirectoryName(_logFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var record = new
        {
            timestampUtc = DateTimeOffset.UtcNow,
            question,
            answer,
            violations = violations.Select(v => new
            {
                v.Code,
                v.Message,
                v.Citation,
                v.Sentence
            })
        };

        var line = JsonSerializer.Serialize(record) + Environment.NewLine;

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await File.AppendAllTextAsync(_logFilePath, line, ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }
}
