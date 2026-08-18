namespace Sellevate.Learning.Features.Assignments.Services.Abstract;

/// <summary>
/// Phase 40.24. Issues whatever repeat waves are due inside one organization
/// (docs/TENANCY/ASSIGNMENTS.md §2.1).
///
/// <para>
/// Scoped and tenant-bound on purpose, exactly like <see cref="IAssignmentDeadlineNoticeService"/>:
/// it is always called with a concrete organization already set on the context by the worker that
/// enumerates them, so every query it runs is filtered by the query filter and the row-level-security
/// policy exactly as a request would be (docs/TENANCY/BACKGROUND_JOBS.md §1).
/// </para>
/// </summary>
public interface IAssignmentRepeatIssueService
{
    /// <summary>Issues the repeats due in the current organization and returns how many were created.</summary>
    Task<int> IssueDueRepeatsAsync(CancellationToken cancellationToken = default);
}
