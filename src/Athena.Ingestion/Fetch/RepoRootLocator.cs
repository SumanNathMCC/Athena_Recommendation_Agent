namespace Athena.Ingestion.Fetch;

public static class RepoRootLocator
{
    public static string Find(string? startDirectory = null)
    {
        var directory = new DirectoryInfo(startDirectory ?? Directory.GetCurrentDirectory());

        while (directory is not null)
        {
            var solutionPath = Path.Combine(directory.FullName, "Athena.sln");
            var manifestPath = Path.Combine(directory.FullName, "corpus", "manifest.json");

            if (File.Exists(solutionPath) && File.Exists(manifestPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the repository root. Expected Athena.sln and corpus/manifest.json.");
    }
}
