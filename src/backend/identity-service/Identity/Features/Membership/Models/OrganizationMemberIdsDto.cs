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
public sealed record OrganizationMemberIdsDto(IReadOnlyList<Guid> UserIds);
