using System.Text.Json;

namespace Sellevate.Learning.Features.Assignments.Models;

/// <summary>
/// Phase 40.23. One assignment as the person who has to do it sees it — the roadmap's "активное
/// задание первым экраном у менеджера, пока не выполнено".
///
/// <para>
/// <b>This is a read of the caller's own progress row, not of the assignment table.</b> An
/// assignment exists for an organization; being asked to do it is the row. So the learner-facing
/// list is driven from <c>AssignmentProgressRecords</c> and an assignment nobody issued to this
/// person is invisible to them however active it is — which is also what makes the endpoint safe
/// without a second authorization gate.
/// </para>
/// </summary>
/// <param name="Status">The caller's own <c>AssignmentProgressStatuses</c> value.</param>
/// <param name="CompletionRule">
/// The threshold, verbatim, so the screen can say "3 разговора с оценкой не ниже 70" instead of
/// "in progress". A manager who cannot see the bar cannot aim at it, and 40.22's whole argument is
/// that the bar is the feature.
/// </param>
public sealed record ActiveAssignmentDto(
    Guid Id,
    string Title,
    string? Goal,
    DateTime? OpensAt,
    DateTime? Deadline,
    JsonElement CompletionRule,
    IReadOnlyList<ActiveAssignmentItemDto> Content,
    string Status,
    int? BestScore,
    int AttemptCount,
    DateTime? FirstOpenedAt,
    DateTime? CompletedAt);

/// <summary>
/// Phase 40.23. One piece of an assignment's work, with just enough resolved for the client to open
/// it.
///
/// <para>
/// <b>No exercise bodies travel here, and that is what makes "no new renderers" true.</b> A
/// <c>lesson_version</c> item carries the lesson it is a snapshot of, so the existing lesson screen
/// plays it with the existing eleven exercise types; a <c>dialog_scenario</c> item carries the
/// ai-service mode key the existing practice screen already starts a session from. The assignment
/// contributes the ordering and the deadline, nothing else.
/// </para>
/// </summary>
/// <param name="Reference">
/// The raw reference as stored: a lesson-version id, a reference-material id, or a dialog mode key.
/// </param>
/// <param name="Title">
/// What to show. Null when the referenced row is gone or is not visible to this caller — an
/// assignment issued against content that was later archived still lists the item rather than
/// silently shrinking, because a manager told to do four things and shown three has no way to ask
/// about the fourth.
/// </param>
/// <param name="LessonId">
/// Set only for <c>lesson_version</c>: the lesson the pinned snapshot belongs to, which is what the
/// learner's existing screens navigate by.
/// </param>
public sealed record ActiveAssignmentItemDto(
    string Kind,
    string Reference,
    int OrderIndex,
    string? Title,
    Guid? LessonId);
