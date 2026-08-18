namespace Sellevate.Learning.Features.ContentAdaptation.Models;

/// <summary>
/// Phase 40.32. One item in the review queue, without the two documents. The list is walked item by
/// item, so it carries only what is needed to decide which item to open next.
/// </summary>
/// <param name="ChangeSummary">The model's sentence about what it changed. Null in review mode.</param>
/// <param name="FindingCount">Review mode: how many things are wrong with this exercise.</param>
/// <param name="HasBlockingFinding">
/// Review mode: at least one finding makes the exercise actively harmful rather than merely weak.
/// Carried in the list because that is the whole reason the severity split exists — a queue of sixty
/// advisory notes must not bury the one that says the correct answer teaches a forbidden promise.
/// </param>
public sealed record ContentAdaptationItemSummaryDto(
    Guid Id,
    Guid ExerciseId,
    Guid LessonId,
    string LessonTitle,
    string ExerciseType,
    int OrderInLesson,
    string Status,
    string? ChangeSummary,
    int FindingCount,
    bool HasBlockingFinding,
    int ChangedFieldCount,
    string? FailureReason,
    DateTime? ResolvedAt);
