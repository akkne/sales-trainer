using Sellevate.Learning.Features.Assignments.Models;

namespace Sellevate.Learning.Features.Assignments.Services.Abstract;

/// <summary>
/// Phase 40.21. The lifecycle of one organization's assignments (docs/TENANCY/ASSIGNMENTS.md §1).
/// Every operation is bounded to the caller's organization by <c>ITenantContext</c>; none of them
/// takes an organization as an argument, and none ever will (docs/TENANCY/TENANCY.md §1.3).
///
/// <para>
/// Payload problems raise <c>AssignmentValidationException</c>; states of an existing row come back
/// as an <c>AssignmentWriteResult</c>, so "there is no such assignment" and "there is one and it is
/// already closed" stay distinguishable all the way to the status code.
/// </para>
/// </summary>
public interface IAssignmentService
{
    Task<IReadOnlyList<AssignmentSummaryDto>> GetAssignmentsAsync(
        string? status = null,
        CancellationToken cancellationToken = default);

    Task<AssignmentDto?> GetAssignmentAsync(
        Guid assignmentId,
        CancellationToken cancellationToken = default);

    Task<AssignmentDto> CreateAsync(
        Guid? actorId,
        CreateAssignmentRequestDto requestDto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the editable fields. On an active assignment the four fields every recorded attempt
    /// was scored against — source type, source reference, content and completion rule — are refused
    /// rather than silently ignored, because silently ignoring an edit is how a РОП comes to believe
    /// they changed a threshold.
    /// </summary>
    Task<AssignmentWriteResult> UpdateAsync(
        Guid assignmentId,
        UpdateAssignmentRequestDto requestDto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues the assignment: <c>draft → active</c>. Refuses an assignment with no content, and
    /// (40.23) resolves its audience into people, writes their progress rows and stages their
    /// notices in the same transaction. Raises <c>AssignmentAudienceUnavailableException</c> when
    /// the roster cannot be read — nothing is written in that case.
    /// </summary>
    Task<AssignmentWriteResult> ActivateAsync(
        Guid assignmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Phase 40.23. Nudges everybody on an active assignment who has not completed it. Returns
    /// <see langword="null"/> when there is no such assignment in the caller's organization.
    ///
    /// <para>
    /// Phase 40.26 gave it a <paramref name="scope"/> — <c>unfinished</c> (the default, and 40.23's
    /// behaviour) or <c>not_started</c>, the set the deadline digest names — and made it consult the
    /// live roster first. It therefore raises <c>AssignmentAudienceUnavailableException</c> when
    /// identity-service cannot be reached, exactly as issuing does: reminding without the roster
    /// means mailing an ex-employee their former employer's homework, and 40.23 refused that in the
    /// sweep for the same reason.
    /// </para>
    /// </summary>
    Task<AssignmentReminderResultDto?> RemindAsync(
        Guid assignmentId,
        string? scope = null,
        CancellationToken cancellationToken = default);

    /// <summary>Ends it: <c>active → closed</c>. There is no way back, by design.</summary>
    Task<AssignmentWriteResult> CloseAsync(
        Guid assignmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a draft. Anything that has been issued is refused: somebody's progress row is the
    /// record that they were asked to do something, and the answer to "we changed our minds" is
    /// closing the assignment, not erasing the fact that it existed.
    /// </summary>
    Task<AssignmentWriteResult> DeleteAsync(
        Guid assignmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Who stands where on one assignment. Returns <see langword="null"/> when the assignment does not
    /// exist or is not visible to the caller, and an empty list until 40.23 issues anything.
    /// </summary>
    Task<IReadOnlyList<AssignmentProgressDto>?> GetProgressAsync(
        Guid assignmentId,
        CancellationToken cancellationToken = default);
}
