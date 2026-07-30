using System.Reflection;
using System.Text.RegularExpressions;

namespace Athena.Plugins.Prompts;

/// <summary>
/// Loads <c>prompts/answer.yaml</c> for grounded answering.
/// </summary>
public static class AnswerPromptLoader
{
    private static readonly Lazy<string> Template = new(LoadTemplate);

    public static string GetTemplate() => Template.Value;

    private static string LoadTemplate()
    {
        var fromFile = TryReadFromFile();
        if (!string.IsNullOrWhiteSpace(fromFile))
        {
            return ExtractTemplateBody(fromFile);
        }

        var fromResource = TryReadFromEmbeddedResource();
        if (!string.IsNullOrWhiteSpace(fromResource))
        {
            return ExtractTemplateBody(fromResource);
        }

        throw new InvalidOperationException(
            "Could not load prompts/answer.yaml. Ensure it is copied to the output directory.");
    }

    private static string? TryReadFromFile()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "prompts", "answer.yaml"),
            Path.Combine(Directory.GetCurrentDirectory(), "prompts", "answer.yaml"),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "Athena.Plugins", "prompts", "answer.yaml")
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }
        }

        return null;
    }

    private static string? TryReadFromEmbeddedResource()
    {
        var assembly = typeof(AnswerPromptLoader).Assembly;
        var name = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("answer.yaml", StringComparison.OrdinalIgnoreCase));
        if (name is null)
        {
            return null;
        }

        using var stream = assembly.GetManifestResourceStream(name);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static string ExtractTemplateBody(string yaml)
    {
        var lines = yaml.Replace("\r\n", "\n").Split('\n');
        var start = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"^\s*template:\s*\|\s*$"))
            {
                start = i + 1;
                break;
            }
        }

        if (start < 0)
        {
            return yaml.Trim();
        }

        var bodyLines = new List<string>();
        for (var i = start; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Length > 0 && !char.IsWhiteSpace(line[0]))
            {
                // Next top-level YAML key.
                break;
            }

            bodyLines.Add(line);
        }

        var minIndent = bodyLines
            .Where(l => l.Length > 0 && !string.IsNullOrWhiteSpace(l))
            .Select(l => l.TakeWhile(char.IsWhiteSpace).Count())
            .DefaultIfEmpty(0)
            .Min();

        var dedented = bodyLines.Select(l =>
            l.Length >= minIndent ? l[minIndent..] : l);
        return string.Join('\n', dedented).Trim();
    }

    public static string Render(string question, string context)
    {
        var template = GetTemplate();
        return template
            .Replace("{{$context}}", context, StringComparison.Ordinal)
            .Replace("{{$question}}", question, StringComparison.Ordinal);
    }
}
