namespace Athena.Recommendation;

/// <summary>
/// One recommended document with a score and a signal-grounded reason.
/// </summary>
public readonly record struct Recommendation(
    string DocId,
    string Title,
    string Reason,
    IReadOnlyList<string> Topics,
    double Score);
