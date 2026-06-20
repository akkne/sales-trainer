namespace Sellevate.Learning.Features.Exercises.Models;

public record ExerciseSubmissionResultDto(
    bool IsCorrect,
    int Score,
    string? Explanation,
    string? AiFeedback,
    int XpEarned,
    IReadOnlyList<string> NewlyUnlockedAchievementKeys
);
