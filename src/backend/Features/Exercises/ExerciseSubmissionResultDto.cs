namespace SalesTrainer.Api.Features.Exercises;

public record ExerciseSubmissionResultDto(
    bool IsCorrect,
    int Score,
    string? Explanation,
    string? AiFeedback,
    int XpEarned
);
