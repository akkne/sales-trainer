namespace Sellevate.Learning.Infrastructure.Ai;

public sealed record AiEvaluationResult(
    bool IsCorrect,
    int Score,
    string? Explanation,
    string? AiFeedback);
