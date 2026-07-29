using System.Text.Json;
using Athena.Core.Records;
using Athena.Ingestion.Chunking;
using Athena.Ingestion.Summarization;

namespace Athena.Ingestion.IngestionArtifacts;

public static class IngestedVectorArtifactWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static IngestedVectorArtifact Build(
        DocumentMetadata metadata,
        DocumentSummaryResult summary,
        string chunkerName,
        IReadOnlyList<ChunkDraft> chunks,
        IReadOnlyList<string> chunkIds,
        IReadOnlyList<ReadOnlyMemory<float>> chunkEmbeddings,
        ReadOnlyMemory<float> docEmbedding)
    {
        if (chunks.Count != chunkIds.Count || chunks.Count != chunkEmbeddings.Count)
        {
            throw new InvalidOperationException("Chunk, id, and embedding counts must match.");
        }

        return new IngestedVectorArtifact
        {
            DocId = metadata.DocId,
            Title = metadata.Title,
            Cluster = metadata.Cluster,
            PublishedOn = metadata.PublishedOn,
            LineageGroup = metadata.LineageGroup,
            PageCount = metadata.PageCountApprox,
            Summary = summary.Summary,
            Topics = summary.Topics.ToList(),
            Chunker = chunkerName,
            IngestedAt = DateTimeOffset.UtcNow,
            DocEmbedding = docEmbedding.ToArray(),
            Chunks = chunks.Select((chunk, index) => new IngestedChunkVector
            {
                ChunkId = chunkIds[index],
                PageNumber = chunk.PageNumber,
                Section = chunk.Section,
                Kind = chunk.Kind,
                Embedding = chunkEmbeddings[index].ToArray()
            }).ToList()
        };
    }

    public static async Task WriteAsync(string path, IngestedVectorArtifact artifact, CancellationToken ct)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, artifact, JsonOptions, ct).ConfigureAwait(false);
    }
}
