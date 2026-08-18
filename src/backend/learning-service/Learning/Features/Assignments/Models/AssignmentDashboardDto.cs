namespace Sellevate.Learning.Features.Assignments.Models;

/// <summary>
/// Phase 40.25. Everything the РОП's screen for one assignment shows: the funnel, the people behind
/// it, and — when the assignment repeats — the same funnel for every wave of the series
/// (docs/TENANCY/ASSIGNMENTS.md §4 and §2.1).
///
/// <para>
/// <b>One endpoint rather than three, because the three answers are read together.</b> A funnel with
/// no names is a number the РОП cannot act on, and a wave's numbers mean nothing except next to the
/// previous wave's — 40.24 built the series precisely so the decay could be seen, and a screen that
/// makes the comparison a second request makes it a comparison nobody performs.
/// </para>
/// </summary>
/// <param name="Assignment">The wave being looked at, in the same shape the list uses.</param>
/// <param name="Funnel">Its funnel.</param>
/// <param name="Rows">Its people, worst standing first — see <see cref="AssignmentDashboardRowDto"/>.</param>
/// <param name="Series">
/// Every wave of the series this assignment belongs to, origin first, each with its own funnel.
/// A single-shot assignment yields exactly one entry — itself — rather than an empty list, so the
/// screen has one shape instead of two.
/// </param>
/// <param name="RosterKnown">
/// Whether identity-service could be asked who still works here. False means every
/// <c>IsActiveMember</c> and every roster count on this response is <see langword="null"/>, and the
/// screen should say "could not check" rather than draw a zero.
/// </param>
public sealed record AssignmentDashboardDto(
    AssignmentSummaryDto Assignment,
    AssignmentFunnelDto Funnel,
    IReadOnlyList<AssignmentDashboardRowDto> Rows,
    IReadOnlyList<AssignmentWaveDto> Series,
    bool RosterKnown);

/// <summary>
/// Phase 40.25. One person on one assignment, named.
///
/// <para>
/// <b>The name comes from <c>UserReplicas</c> and is nullable.</b> learning-service does not own
/// identities; the replica is a projection of <c>user.updated</c> and a person who has never
/// triggered one has no row yet. A missing name is shown as a missing name — inventing "Unknown
/// user" in the API would make the screen unable to tell "we have not heard about them" from "their
/// display name is literally that".
/// </para>
///
/// <para>
/// <b><see cref="IsActiveMember"/> is what closes 40.23's open cost.</b> A person who left keeps
/// their row and reads as <c>not_started</c> forever; this flag is how the screen stops counting
/// them against a team that has not failed at anything. Null means the roster could not be read.
/// </para>
/// </summary>
public sealed record AssignmentDashboardRowDto(
    Guid UserId,
    string? DisplayName,
    string Status,
    int? BestScore,
    int AttemptCount,
    DateTime? FirstOpenedAt,
    DateTime? CompletedAt,
    bool? IsActiveMember);

/// <summary>
/// Phase 40.25. One wave of a repeat series with its own funnel, so the decay 40.24 exists to
/// surface is a comparison the screen can draw (docs/TENANCY/ASSIGNMENTS.md §2.1).
/// </summary>
/// <param name="WaveIndex">
/// 0 for the origin, then the 1-based <c>RepeatWaveIndex</c> of each repeat. The origin is given
/// index 0 rather than 1 so that "wave 2" on the screen is the same number as the offset ordinal the
/// РОП configured.
/// </param>
public sealed record AssignmentWaveDto(
    Guid AssignmentId,
    int WaveIndex,
    string Status,
    DateTime? ActivatedAt,
    DateTime? Deadline,
    AssignmentFunnelDto Funnel);
