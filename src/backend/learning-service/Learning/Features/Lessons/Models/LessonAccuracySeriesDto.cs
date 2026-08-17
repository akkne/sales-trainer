namespace Sellevate.Learning.Features.Lessons.Models;

/// <summary>
/// Phase 40.16. The accuracy of one lesson for the calling organization, split into the segments a
/// dashboard may draw as one continuous line.
/// </summary>
/// <param name="Segments">
/// Ordered by version number, oldest first. Empty for a lesson nobody has published yet.
/// </param>
/// <param name="UnversionedAttempts">
/// Attempts that carry no <c>LessonVersionId</c>: everything recorded before phase 40.16, until
/// <c>docs/TENANCY/sql/40.16_progress_version_backfill.sql</c> is run. Reported as its own bucket
/// rather than folded into version 1, because nobody can prove which content those answers were
/// scored against — merging them silently would be the same lie this phase exists to stop, told by
/// the fix instead of by the bug.
/// </param>
public sealed record LessonAccuracySeriesDto(
    Guid LessonId,
    IReadOnlyList<LessonAccuracySegmentDto> Segments,
    LessonAttemptStatisticsDto UnversionedAttempts);
