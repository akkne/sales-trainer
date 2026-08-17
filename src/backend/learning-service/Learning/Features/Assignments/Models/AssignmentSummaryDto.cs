namespace Sellevate.Learning.Features.Assignments.Models;

/// <summary>
/// Phase 40.21. One row of the РОП's assignment list: enough to decide which assignment to open, and
/// nothing that requires parsing a jsonb column.
///
/// <para>
/// The four counts are the funnel of docs/TENANCY/ASSIGNMENTS.md §4 in its smallest useful form. They
/// are all zero until 40.23 issues anything, which is the honest answer rather than a hidden one.
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
    int ContentItemCount,
    int AssignedCount,
    int StartedCount,
    int CompletedCount,
    int FailedThresholdCount,
    Guid? CreatedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt);
