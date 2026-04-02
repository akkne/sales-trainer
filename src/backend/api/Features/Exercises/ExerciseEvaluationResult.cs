namespace SalesTrainer.Api.Features.Exercises;

public record ExerciseEvaluationResult(
    bool IsCorrect,
    int Score,
    string? Explanation,
    string? AiFeedback
);
