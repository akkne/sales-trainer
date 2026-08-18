namespace Sellevate.Learning.Features.Assignments.Models;

/// <summary>
/// Phase 40.21. One row of the РОП's assignment list: enough to decide which assignment to open, and
/// nothing that requires parsing a jsonb column.
///
/// <para>
/// The four counts are the funnel of docs/TENANCY/ASSIGNMENTS.md §4 in its smallest useful form.
/// </para>
///
/// <para>
/// Phase 40.24. <see cref="RepeatOfAssignmentId"/> and <see cref="RepeatWaveIndex"/> are what let a
/// list of assignments be read as a list of <b>series</b>: a repeat is its own row with its own
/// funnel, and these two fields are how the screen groups the waves back together instead of showing
/// the same training three times as three unrelated assignments. Both are null on an assignment a
/// human created.
/// </para>
/// </summary>
public sealed record AssignmentSummaryDto(
    Guid Id,
    string Title,
    string SourceType,
    string Status,
    string AudienceKind,
    DateTime? OpensAt,
    DateTime? Deadline,
    bool HasRepeatSchedule,
    Guid? RepeatOfAssignmentId,
    int? RepeatWaveIndex,
    int ContentItemCount,
    int AssignedCount,
    int StartedCount,
    int CompletedCount,
    int FailedThresholdCount,
    Guid? CreatedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt);
