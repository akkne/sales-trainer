namespace Sellevate.Learning.Features.Lessons.Models;

/// <summary>
/// Phase 40.16. What one point of the accuracy chart is made of.
/// <see cref="Accuracy"/> is a fraction in 0..1, not a percentage, and is 0 when
/// <see cref="AttemptCount"/> is 0 — a segment with no attempts is reported rather than omitted,
/// because "nobody has answered this version yet" and "this version does not exist" are different
/// answers and the caller must be able to tell them apart.
/// </summary>
public sealed record LessonAttemptStatisticsDto(
    int AttemptCount,
    int CorrectAttemptCount,
    double Accuracy,
    double AverageScore,
    DateTime? FirstAttemptAt,
    DateTime? LastAttemptAt)
{
    public static LessonAttemptStatisticsDto Empty { get; } =
        new(AttemptCount: 0, CorrectAttemptCount: 0, Accuracy: 0, AverageScore: 0, FirstAttemptAt: null, LastAttemptAt: null);
}
