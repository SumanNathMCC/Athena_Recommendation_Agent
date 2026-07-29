using System.Text;
using Athena.Core.Records;
using Athena.Ingestion.Summarization;

namespace Athena.Ingestion.Chunking;

public static class ChunkedMarkdownWriter
{
    public static string Write(
        DocumentMetadata metadata,
        string chunkerName,
        DocumentSummaryResult summary,
        IReadOnlyList<ChunkDraft> chunks)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentException.ThrowIfNullOrWhiteSpace(chunkerName);
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(chunks);

        var builder = new StringBuilder();
        builder.AppendLine("---");
        builder.AppendLine($"docId: {YamlValue(metadata.DocId)}");
        builder.AppendLine($"title: {YamlValue(metadata.Title)}");
        builder.AppendLine($"cluster: {YamlValue(metadata.Cluster)}");
        builder.AppendLine($"publishedOn: {metadata.PublishedOn:yyyy-MM-dd}");
        builder.AppendLine($"chunker: {YamlValue(chunkerName)}");
        builder.AppendLine($"chunkedAt: {DateTimeOffset.UtcNow:O}");
        builder.AppendLine($"chunkCount: {chunks.Count}");
        builder.AppendLine($"summary: {YamlValue(summary.Summary)}");
        builder.AppendLine("topics:");
        foreach (var topic in summary.Topics)
        {
            builder.AppendLine($"  - {YamlValue(topic)}");
        }
        builder.AppendLine("---");
        builder.AppendLine();

        for (var index = 0; index < chunks.Count; index++)
        {
            var chunk = chunks[index];
            var chunkId = BuildChunkId(metadata.DocId, chunk, index);
            var kind = chunk.Kind switch
            {
                ChunkKind.Table => "table",
                ChunkKind.OcrProse => "ocrprose",
                _ => "prose"
            };

            builder.AppendLine(
                $"<!-- chunk: id={YamlValue(chunkId)} page={chunk.PageNumber} " +
                $"section={YamlValue(chunk.Section)} kind={kind} -->");
            builder.AppendLine(chunk.Text.Trim());
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    internal static string BuildChunkId(string docId, ChunkDraft chunk, int index)
    {
        var kindPrefix = chunk.Kind switch
        {
            ChunkKind.Table => "t",
            ChunkKind.OcrProse => "o",
            _ => "p"
        };

        return $"{docId}-{kindPrefix}{chunk.PageNumber:D4}-{index:D4}";
    }

    private static string YamlValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        return value.Contains(':', StringComparison.Ordinal) ||
               value.Contains('\"', StringComparison.Ordinal) ||
               value.Contains('\n', StringComparison.Ordinal)
            ? $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\""
            : $"\"{value}\"";
    }
}
