namespace Sellevate.Organization.Infrastructure.Identity;

/// <summary>The bootstrap invite <c>identity-service</c> minted or already had pending, as reported
/// by <c>POST internal/organizations/{organizationId}/bootstrap-admin</c>.</summary>
public sealed record IdentityBootstrapAdminResult(Guid InviteId, string Email, DateTime ExpiresAt);
