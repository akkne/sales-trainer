namespace Sellevate.Learning.Features.Assignments.Services.Abstract;

/// <summary>
/// Phase 40.23. Warns everybody who has not finished an assignment whose deadline is close, inside
/// one organization.
///
/// <para>
/// Scoped and tenant-bound on purpose: it is always called with a concrete organization already set
/// on the context by the worker that enumerates them, so every query it runs is filtered by the
/// query filter and the row-level-security policy exactly as a request would be
/// (docs/TENANCY/BACKGROUND_JOBS.md §1).
/// </para>
/// </summary>
public interface IAssignmentDeadlineNoticeService
{
    /// <summary>Publishes the notices due in the current organization and returns how many.</summary>
    Task<int> PublishDueNoticesAsync(CancellationToken cancellationToken = default);
}
