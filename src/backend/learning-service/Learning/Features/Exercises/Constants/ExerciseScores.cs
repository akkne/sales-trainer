namespace Sellevate.Learning.Features.Exercises.Constants;

/// <summary>
/// The bounds of the score an evaluation strategy returns.
///
/// <para>
/// Percentage points, and persisted: <c>UserExerciseAttempt.Score</c> and
/// <c>UserLessonProgress.BestScore</c> hold these values, and the assignment threshold rule compares
/// against them. That makes the scale part of the data rather than a display choice — a strategy that
/// returned a different maximum would silently change what "≥80%" means for every existing
/// assignment.
/// </para>
///
/// <para>
/// A deterministic strategy is all-or-nothing per exercise; the partial scores come from strategies
/// that grade a proportion (match-pairs, categorize) and from the AI grader, which returns its own
/// value on the same scale.
/// </para>
/// </summary>
public static class ExerciseScores
{
    public const int Minimum = 0;

    public const int Maximum = 100;
}
