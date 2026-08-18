namespace Sellevate.Identity.Features.PlatformAdmin.Models;

/// <summary>
/// The created bootstrap invite. <see cref="Token"/> is the raw single-use invite token, returned
/// exactly once for the same reason as in <c>CreatedInviteDto</c> — only its hash is stored.
/// </summary>
public sealed record BootstrapOrganizationAdminResponseDto(
    Guid InviteId,
    OrganizationReferenceDto Organization,
    string Email,
    DateTime ExpiresAt,
    string Token);
