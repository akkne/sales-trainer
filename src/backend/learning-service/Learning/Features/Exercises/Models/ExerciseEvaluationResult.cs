namespace Sellevate.Learning.Features.Exercises.Models;

public record ExerciseEvaluationResult(
    bool IsCorrect,
    int Score,
    string? Explanation,
    string? AiFeedback,
    /// <summary>
    /// The correct answer to reveal in the submission result (docs/AUDIT_PROD.md X-3/X-6/X-8). Null
    /// for exercise types with nothing to reveal in this shape.
    /// </summary>
    ExerciseCorrectAnswerDto? CorrectAnswer = null
);
