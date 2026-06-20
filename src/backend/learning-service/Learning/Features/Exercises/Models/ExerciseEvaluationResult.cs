namespace Sellevate.Learning.Features.Exercises.Models;

public record ExerciseEvaluationResult(
    bool IsCorrect,
    int Score,
    string? Explanation,
    string? AiFeedback
);
