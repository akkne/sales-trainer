namespace Sellevate.Learning.Features.Assignments.Models;

/// <summary>
/// Phase 40.25. The funnel of docs/TENANCY/ASSIGNMENTS.md §4: assigned → started → completed →
/// met the threshold, for exactly one assignment.
///
/// <para>
/// <b>The four roadmap counts are five here, and the fifth is the point.</b>
/// <see cref="FailedThresholdCount"/> is not a subset of <see cref="CompletedCount"/> — it is the
/// people who finished the work and stayed under the bar, which §1.1 calls the most valuable row on
/// the screen. A four-stage funnel that ends at "completed" hides them among the people who never
/// started, and that is the exact failure 40.22 separated the two states to prevent.
/// </para>
///
/// <para>
/// <b>Why the roster counts are nullable.</b> 40.23 left somebody who leaves the company holding
/// their progress row — the row is the record that they were asked, and deleting it would rewrite
/// history — with the honest cost that a leaver reads as "not started" forever. Answering that needs
/// identity-service, which can be unavailable. A dashboard is a read: it degrades to "we could not
/// check who still works here" rather than failing, and <see langword="null"/> is how it says so.
/// Zero would be a claim, and it would be the wrong one.
/// </para>
/// </summary>
/// <param name="AssignedCount">Progress rows in existence — everybody who was ever asked.</param>
/// <param name="NotStartedCount">Rows still at <c>not_started</c>, leavers included.</param>
/// <param name="StartedCount">
/// Everybody who has done at least one piece of graded work: <c>in_progress</c>, <c>completed</c>
/// and <c>failed_threshold</c> together. A person who tried and failed has started.
/// </param>
/// <param name="CompletedCount">Rows at <c>completed</c> — the threshold was met.</param>
/// <param name="FailedThresholdCount">Rows at <c>failed_threshold</c> — finished, under the bar.</param>
/// <param name="LeftOrganizationCount">
/// How many of the assigned no longer hold an active membership here, or <see langword="null"/> when
/// the roster could not be read.
/// </param>
/// <param name="AssignedActiveCount">
/// <see cref="AssignedCount"/> minus <see cref="LeftOrganizationCount"/> — the denominator a РОП
/// should actually judge the team by. Null under the same condition.
/// </param>
public sealed record AssignmentFunnelDto(
    int AssignedCount,
    int NotStartedCount,
    int StartedCount,
    int CompletedCount,
    int FailedThresholdCount,
    int? LeftOrganizationCount,
    int? AssignedActiveCount);
