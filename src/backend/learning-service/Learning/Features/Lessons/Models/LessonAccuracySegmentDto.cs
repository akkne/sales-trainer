namespace Sellevate.Learning.Features.Lessons.Models;

/// <summary>
/// Phase 40.16. One continuous run of the accuracy chart: every lesson version from
/// <see cref="StartVersionNumber"/> up to <see cref="EndVersionNumber"/> whose content changes were
/// declared cosmetic, aggregated into a single number.
///
/// <para>
/// A segment ends where the next publish was flagged <c>is_breaking</c>. That is the whole point of
/// the flag (docs/TENANCY/CONTENT_MODEL.md §2.4): a fixed comma must not put a step in the chart, and
/// a changed correct answer must not be averaged together with the answers people gave before it
/// changed. Both failures cost the same thing — the РОП stops believing the number.
/// </para>
/// </summary>
public sealed record LessonAccuracySegmentDto(
    int StartVersionNumber,
    int EndVersionNumber,
    IReadOnlyList<int> VersionNumbers,
    IReadOnlyList<Guid> VersionIds,
    bool StartsAtBreakingChange,
    LessonAttemptStatisticsDto Statistics);
