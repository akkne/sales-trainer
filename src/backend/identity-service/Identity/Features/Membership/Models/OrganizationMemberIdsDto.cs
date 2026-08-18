namespace Sellevate.Identity.Features.Membership.Models;

/// <summary>
/// Phase 40.23. Who currently works at the calling organization, as opaque identifiers.
///
/// <para>
/// <b>Ids and nothing else, deliberately.</b> learning-service — the only caller — needs the set to
/// resolve an assignment's audience into progress rows, and it already holds names and avatars in
/// its own platform-global <c>UserReplicas</c>. Returning display names or emails here would put an
/// employee directory behind a shared-secret header for no gain.
/// </para>
/// </summary>
/// <param name="UserIds">
/// Active memberships only. A deactivated membership (Phase 40.7 — leaving is never a row deletion)
/// is absent, which is what stops a new assignment from being issued to somebody who has left.
/// </param>
/// <param name="AdministratorUserIds">
/// Phase 40.26. The subset of <paramref name="UserIds"/> holding <c>TenancyAdmin</c> or
/// <c>TenancySuperAdmin</c> — the people a notice about the organization's own problems is addressed
/// to (docs/TENANCY/ASSIGNMENTS.md §5).
///
/// <para>
/// <b>A second list of ids, not a role per member.</b> 40.25 refused to enumerate administrators at
/// all and recorded why: widening this route is a security-surface change and deserved its own
/// decision rather than a side effect. This is that decision, taken as narrowly as the question
/// allows. A <c>role</c> field on every member would turn one internal route into an organization's
/// role directory, readable in full by any service holding the shared secret; the callers'
/// actual question is «кому писать про эту организацию», and one extra list of ids answers exactly
/// it and nothing else.
/// </para>
///
/// <para>
/// It is a subset rather than a disjoint list, so a caller that ignores it keeps the roster it
/// always had. Never null on the wire — an organization with no administrator sends an empty list,
/// which is a real and different answer from "this service does not report administrators" (what an
/// older identity-service produces, and what learning-service distinguishes).
/// </para>
/// </param>
public sealed record OrganizationMemberIdsDto(
    IReadOnlyList<Guid> UserIds,
    IReadOnlyList<Guid> AdministratorUserIds);
