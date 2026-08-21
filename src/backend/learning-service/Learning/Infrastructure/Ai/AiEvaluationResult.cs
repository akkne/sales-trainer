namespace Sellevate.Learning.Infrastructure.Ai;

public sealed record AiEvaluationResult(
    bool IsCorrect,
    int Score,
    string? Explanation,
    string? AiFeedback,
    /// <summary>
    /// <c>spot_mistake</c> only, mirrors ai-service's own field of the same name
    /// (docs/AUDIT_PROD.md X-8). Null for every other AI-graded type.
    /// </summary>
    int? CorrectLineIndex = null);
