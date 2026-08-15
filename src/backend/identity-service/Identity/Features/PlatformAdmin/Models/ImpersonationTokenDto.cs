namespace Sellevate.Identity.Features.PlatformAdmin.Models;

/// <summary>
/// The answer to <c>POST /admin/platform/impersonation</c>. There is no refresh token on purpose:
/// an impersonation session ends when <see cref="ExpiresAt"/> passes and can only be extended by
/// asking again, which writes another audit row.
/// </summary>
public sealed record ImpersonationTokenDto(
    string AccessToken,
    DateTime ExpiresAt,
    Guid ImpersonationId,
    OrganizationReferenceDto Organization);
