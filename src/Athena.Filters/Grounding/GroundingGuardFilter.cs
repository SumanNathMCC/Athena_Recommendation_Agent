using Athena.Retrieval;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Athena.Filters.Grounding;

/// <summary>
/// Part C: after <c>answer_question</c>, validate citations against retrieved passages.
/// On failure, replace the answer with INSUFFICIENT_CONTEXT and log the violation.
/// </summary>
public sealed class GroundingGuardFilter : IFunctionInvocationFilter
{
    private readonly IRetrievedContextAccessor _retrievedContext;
    private readonly ICitationViolationLogger _logger;
    private readonly ILogger<GroundingGuardFilter> _trace;

    public GroundingGuardFilter(
        IRetrievedContextAccessor retrievedContext,
        ICitationViolationLogger logger,
        ILogger<GroundingGuardFilter> trace)
    {
        _retrievedContext = retrievedContext;
        _logger = logger;
        _trace = trace;
    }

    public async Task OnFunctionInvocationAsync(
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, Task> next)
    {
        await next(context).ConfigureAwait(false);

        if (!IsAnswerQuestion(context.Function))
        {
            return;
        }

        var answer = context.Result?.GetValue<string>()?.Trim();
        if (string.IsNullOrWhiteSpace(answer))
        {
            return;
        }

        var passages = _retrievedContext.LastPassages;
        var check = CitationGroundingValidator.Validate(answer, passages);
        if (check.IsValid)
        {
            return;
        }

        var question = ResolveQuestion(context);

        _trace.LogWarning(
            "GroundingGuard rejected answer_question ({ViolationCount} violation(s)).",
            check.Violations.Count);

        try
        {
            await _logger.LogAsync(question, answer, check.Violations).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _trace.LogError(ex, "Failed to write citation-violations.jsonl");
        }

        context.Result = new FunctionResult(context.Function, CitationGroundingValidator.InsufficientContext);
    }

    private string ResolveQuestion(FunctionInvocationContext context)
    {
        if (!string.IsNullOrWhiteSpace(_retrievedContext.LastQuery))
        {
            return _retrievedContext.LastQuery!;
        }

        if (context.Arguments.TryGetValue("question", out var value) && value is not null)
        {
            return value.ToString() ?? string.Empty;
        }

        return string.Empty;
    }

    private static bool IsAnswerQuestion(KernelFunction function) =>
        string.Equals(function.Name, "answer_question", StringComparison.OrdinalIgnoreCase);
}
