using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Athena.Agent;

/// <summary>Agent metadata and instructions loaded from a markdown file with YAML front matter.</summary>
public sealed class AgentPromptDefinition
{
    public string Name { get; set; } = string.Empty;

    public string Instructions { get; set; } = string.Empty;

    public double Temperature { get; set; } = 0.1;
}

/// <summary>
/// Loads agent prompts the same way as RegRagAgent: markdown with YAML front matter.
/// </summary>
public static class AgentPromptLoader
{
    public static AgentPromptDefinition Load(string contentRootPath, string relativePath)
    {
        var path = ResolvePath(contentRootPath, relativePath);
        if (path is null)
        {
            throw new FileNotFoundException(
                $"Agent prompt file not found: '{relativePath}' " +
                $"(searched under '{contentRootPath}' and '{AppContext.BaseDirectory}').");
        }

        var text = File.ReadAllText(path);
        var yaml = ExtractYamlFrontMatter(text);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var definition = deserializer.Deserialize<AgentPromptDefinition>(yaml);
        if (definition is null || string.IsNullOrWhiteSpace(definition.Name))
        {
            throw new InvalidOperationException($"Agent prompt file '{path}' is missing a valid 'name' field.");
        }

        if (string.IsNullOrWhiteSpace(definition.Instructions))
        {
            throw new InvalidOperationException($"Agent prompt file '{path}' is missing 'instructions'.");
        }

        return definition;
    }

    private static string? ResolvePath(string contentRootPath, string relativePath)
    {
        var candidates = new[]
        {
            Path.Combine(contentRootPath, relativePath),
            Path.Combine(AppContext.BaseDirectory, relativePath),
            Path.Combine(AppContext.BaseDirectory, "Agents", Path.GetFileName(relativePath))
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string ExtractYamlFrontMatter(string text)
    {
        if (!text.StartsWith("---", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Agent prompt file must begin with YAML front matter (---).");
        }

        var end = text.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (end < 0)
        {
            throw new InvalidOperationException(
                "Agent prompt file is missing closing YAML front matter delimiter (---).");
        }

        return text[3..end].Trim();
    }
}
