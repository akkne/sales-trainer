using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Common.Constants;

namespace Sellevate.Learning.Features.Assignments.Models;

/// <summary>
/// Phase 40.21. Where one person stands on one assignment (docs/TENANCY/ASSIGNMENTS.md §1).
///
/// <para>
/// <b>The row's existence is the record that the assignment was issued to this person.</b> That is
/// why 40.23 writes one per resolved recipient at issue time instead of creating rows lazily on first
/// open: "who has not started" is the single most actionable question the РОП asks (§5, and roadmap
/// 40.26 is built entirely on it), and it is a query over rows that exist, not an inference from rows
/// that do not.
/// </para>
///
/// <para>
/// <b>Nothing writes this table in 40.21.</b> The fan-out is 40.23 and the threshold evaluation that
/// decides between <c>completed</c> and <c>failed_threshold</c> is 40.22. The table ships now because
/// the schema is this block's deliverable — see docs/DONT_FORGET.md.
/// </para>
/// </summary>
public sealed class AssignmentProgress : ITenantScoped
{
    public Guid Id { get; set; }

    /// <summary>
    /// Denormalized copy of the owning <see cref="Models.Assignment"/>'s organization. Denormalized on
    /// purpose — a row-level-security policy can only compare columns of the row it is filtering, so
    /// the isolation boundary needs the value here and not one join away (docs/TENANCY/TENANCY.md
    /// §1.5). An assignment never changes owner, so the copy cannot drift.
    /// </summary>
    public Guid OrganizationId { get; set; }

    public Guid AssignmentId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>One of <see cref="AssignmentProgressStatuses"/>.</summary>
    public string Status { get; set; } = AssignmentProgressStatuses.NotStarted;

    /// <summary>
    /// The best result this person has reached, 0–100, or null if they have never been scored. Best
    /// rather than latest because the completion rule is a threshold: somebody who cleared it once has
    /// cleared it, and a later worse attempt is practice, not a demotion.
    /// </summary>
    public int? BestScore { get; set; }

    /// <summary>
    /// How many times they tried. Carries the whole weight of the <c>failed_threshold</c> state: "did
    /// not finish" and "tried four times and did not reach the bar" are the same status without it,
    /// and they call for opposite reactions from the РОП.
    /// </summary>
    public int AttemptCount { get; set; }

    public DateTime? FirstOpenedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public Assignment? Assignment { get; set; }
}
