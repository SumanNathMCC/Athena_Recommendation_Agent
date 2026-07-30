using Athena.Filters.Grounding;
using Athena.Retrieval;

namespace Athena.Tests.Filters;

public sealed class CitationGroundingValidatorTests
{
    [Fact]
    public void Validate_InsufficientContext_IsAllowed()
    {
        var result = CitationGroundingValidator.Validate(
            "INSUFFICIENT_CONTEXT",
            Array.Empty<Passage>());

        Assert.True(result.IsValid);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public void Validate_MatchingCitation_Passes()
    {
        var passages = new[]
        {
            new Passage("c1", "A1", "Principles for Operational Resilience", 11, "Sec", "text", 1)
        };

        var answer =
            "Tolerance for disruption is defined as the maximum acceptable disruption. " +
            "[Principles for Operational Resilience, p.11]";

        var result = CitationGroundingValidator.Validate(answer, passages);
        Assert.True(result.IsValid);
        Assert.Single(result.Citations);
    }

    [Fact]
    public void Validate_UnknownCitation_Fails()
    {
        var passages = new[]
        {
            new Passage("c1", "A1", "Principles for Operational Resilience", 11, "Sec", "text", 1)
        };

        var answer = "Something invented happened. [Fake Paper, p.99]";
        var result = CitationGroundingValidator.Validate(answer, passages);

        Assert.False(result.IsValid);
        Assert.Contains(result.Violations, v => v.Code == "unknown_citation");
    }

    [Fact]
    public void Validate_MissingCitation_Fails()
    {
        var passages = new[]
        {
            new Passage("c1", "A1", "Principles for Operational Resilience", 11, "Sec", "text", 1)
        };

        var answer = "Banks must define impact tolerances for critical operations without citing anything.";
        var result = CitationGroundingValidator.Validate(answer, passages);

        Assert.False(result.IsValid);
        Assert.Contains(result.Violations, v => v.Code == "missing_citation");
    }

    [Fact]
    public void ParseCitations_ExtractsTitleAndPage()
    {
        var citations = CitationGroundingValidator.ParseCitations(
            "Claim one. [Dense Passage Retrieval for Open-Domain Question Answering, p.3]");

        Assert.Single(citations);
        Assert.Equal("Dense Passage Retrieval for Open-Domain Question Answering", citations[0].Title);
        Assert.Equal(3, citations[0].PageNumber);
    }
}
