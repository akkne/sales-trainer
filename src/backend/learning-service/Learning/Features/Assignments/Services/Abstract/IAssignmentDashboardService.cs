using Sellevate.Learning.Features.Assignments.Models;

namespace Sellevate.Learning.Features.Assignments.Services.Abstract;

/// <summary>
/// Phase 40.25. The read side of an assignment — what the РОП opens on Monday
/// (docs/TENANCY/ASSIGNMENTS.md §4).
///
/// <para>
/// <b>Separate from <see cref="IAssignmentService"/> on purpose.</b> That interface is the
/// lifecycle: create, edit, issue, remind, close. This one writes nothing at all, and keeping the
/// two apart is what lets every method here run inside a read-only tenant scope and lets a reviewer
/// see at a glance that opening a dashboard cannot change an assignment.
/// </para>
/// </summary>
public interface IAssignmentDashboardService
{
    /// <summary>
    /// The funnel, the named people behind it and the whole repeat series this assignment belongs
    /// to. Returns <see langword="null"/> when there is no such assignment in the caller's
    /// organization.
    /// </summary>
    Task<AssignmentDashboardDto?> GetDashboardAsync(
        Guid assignmentId,
        CancellationToken cancellationToken = default);
}
