using Athena.Plugins.Prompts;

namespace Athena.Tests.Plugins;

public sealed class AnswerPromptLoaderTests
{
    [Fact]
    public void ExtractTemplateBody_DedentsYamlBlock()
    {
        var yaml =
            """
            name: answer_question
            template: |
              Line one
              Line two {{$context}}
            template_format: semantic-kernel
            """;

        var body = AnswerPromptLoader.ExtractTemplateBody(yaml);
        Assert.Contains("Line one", body);
        Assert.Contains("{{$context}}", body);
        Assert.DoesNotContain("template_format", body);
    }

    [Fact]
    public void GetTemplate_LoadsAnswerYaml()
    {
        var template = AnswerPromptLoader.GetTemplate();
        Assert.Contains("INSUFFICIENT_CONTEXT", template);
        Assert.Contains("{{$context}}", template);
        Assert.Contains("{{$question}}", template);
        Assert.Contains("[Title, p.N]", template);
    }
}
