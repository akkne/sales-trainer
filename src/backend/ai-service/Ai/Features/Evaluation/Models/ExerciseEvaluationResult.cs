namespace Sellevate.Ai.Features.Evaluation.Models;

public record ExerciseEvaluationResult(
    bool IsCorrect,
    int Score,
    string? Explanation,
    string? AiFeedback,
    /// <summary>
    /// <c>spot_mistake</c> only: index of the dialogue line that is the planted mistake, revealed to
    /// learning-service now that grading has already needed it (docs/AUDIT_PROD.md X-8). Null for
    /// every other AI-graded type.
    /// </summary>
    int? CorrectLineIndex = null
);
