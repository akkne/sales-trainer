using Sellevate.Learning.Features.Lessons.Models;

namespace Sellevate.Learning.Features.Lessons.Services.Abstract;

/// <summary>
/// Phase 40.16. Reads historical attempts through the lesson versions they were scored against —
/// the consumer that finally gives <c>is_breaking</c> a meaning (docs/TENANCY/CONTENT_MODEL.md §2.4).
/// 40.15 recorded the flag on every publish and nothing read it.
/// </summary>
public interface ILessonAccuracyService
{
    /// <summary>
    /// Returns <see langword="null"/> when the lesson does not exist or is not visible to the
    /// caller's organization — the tenancy layer makes those two indistinguishable on purpose.
    /// Everything counted is the caller's own organization's attempts: the row-level-security policy
    /// on <c>UserExerciseAttempts</c> is plain equality, so there is no way for one customer's
    /// numbers to enter another's chart even for a lesson both of them study.
    /// </summary>
    Task<LessonAccuracySeriesDto?> GetAccuracySeriesAsync(
        Guid lessonId,
        CancellationToken cancellationToken = default);
}
