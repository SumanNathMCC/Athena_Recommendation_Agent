namespace Athena.Retrieval;

/// <summary>
/// Passage-level retrieval hit returned by hybrid search.
/// </summary>
public sealed record Passage(
    string ChunkId,
    string DocId,
    string Title,
    int PageNumber,
    string Section,
    string Text,
    double Score);
