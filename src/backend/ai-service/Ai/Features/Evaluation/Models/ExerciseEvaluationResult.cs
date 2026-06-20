namespace Sellevate.Ai.Features.Evaluation.Models;

public record ExerciseEvaluationResult(
    bool IsCorrect,
    int Score,
    string? Explanation,
    string? AiFeedback
);
