namespace Sellevate.Identity.Features.Invites.Models;

/// <summary>
/// One invite as the organization's admin panel lists it.
///
/// <para>
/// There is deliberately no <c>Token</c> here. The raw token is returned exactly once, at creation
/// (<see cref="CreatedInviteDto"/>), and the database keeps only its hash — a listing endpoint that
/// could hand it out again would undo that and turn any admin read into a way to take over a pending
/// invitee's account.
/// </para>
///
/// <para>
/// <see cref="Status"/> is derived, not stored: the row carries <c>AcceptedAt</c>, <c>RevokedAt</c>
/// and <c>ExpiresAt</c>, and the four states follow from them. Deriving it server-side keeps the
/// clock that decides "expired" on one machine instead of on every browser.
/// </para>
/// </summary>
public sealed record InviteSummaryDto(
    Guid Id,
    string Email,
    string Role,
    string Status,
    Guid? InvitedBy,
    DateTime CreatedAt,
    DateTime ExpiresAt);
