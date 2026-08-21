namespace Sellevate.Learning.Features.Exercises.Models;

public record ExerciseSubmissionResultDto(
    bool IsCorrect,
    int Score,
    string? Explanation,
    string? AiFeedback,
    int XpEarned,
    IReadOnlyList<string> NewlyUnlockedAchievementKeys,
    /// <summary>
    /// The correct answer, revealed now that the learner has answered (docs/AUDIT_PROD.md
    /// X-3/X-6/X-8). Null for exercise types with nothing to reveal in this shape, and for a skipped
    /// submission.
    /// </summary>
    ExerciseCorrectAnswerDto? CorrectAnswer = null
);
