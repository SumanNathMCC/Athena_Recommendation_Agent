namespace Athena.Ingestion.Pipeline;

internal static class CorpusPaths
{
    public const string ExtractedDirectory = "corpus/extracted";
    public const string IngestedDirectory = "corpus/ingested";

    public static string GetPdfPath(string repoRoot, string downloadDirectory, string localFile) =>
        Path.Combine(
            ResolveUnderRepo(repoRoot, downloadDirectory),
            localFile);

    public static string GetExtractedPath(string repoRoot, string docId) =>
        Path.Combine(
            ResolveUnderRepo(repoRoot, ExtractedDirectory),
            $"{docId}.md");

    public static string GetIngestedPath(string repoRoot, string docId) =>
        Path.Combine(
            ResolveUnderRepo(repoRoot, IngestedDirectory),
            $"{docId}.md");

    public static string GetIngestedVectorsPath(string repoRoot, string docId) =>
        Path.Combine(
            ResolveUnderRepo(repoRoot, IngestedDirectory),
            $"{docId}.vectors.json");

    public static string ResolveUnderRepo(string repoRoot, string relativePath)
    {
        var rootPath = Path.GetFullPath(repoRoot);
        var resolved = Path.GetFullPath(
            Path.Combine(rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));

        if (!resolved.StartsWith(rootPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            !resolved.Equals(rootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Path '{relativePath}' must resolve inside the repository root.");
        }

        return resolved;
    }

    public static void EnsureArtifactDirectories(string repoRoot)
    {
        Directory.CreateDirectory(ResolveUnderRepo(repoRoot, ExtractedDirectory));
        Directory.CreateDirectory(ResolveUnderRepo(repoRoot, IngestedDirectory));
    }
}
