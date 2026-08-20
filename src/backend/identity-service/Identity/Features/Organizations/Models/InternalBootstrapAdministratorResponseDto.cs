namespace Sellevate.Identity.Features.Organizations.Models;

/// <summary>
/// The bootstrap invite — freshly minted, or the pending one already found — for organization-service
/// to relay back as <c>inviteId</c>/<c>inviteEmail</c>/<c>inviteExpiresAt</c>. No raw token: unlike
/// the JWT-facing <c>BootstrapOrganizationAdminResponseDto</c>, nobody downstream of this call needs
/// to build an accept link — the invite email identity-service already sent carries it.
/// </summary>
public sealed record InternalBootstrapAdministratorResponseDto(Guid InviteId, string Email, DateTime ExpiresAt);
