using Sellevate.Identity.Features.PlatformAdmin.Models;

namespace Sellevate.Identity.Features.PlatformAdmin.Services.Abstract;

/// <summary>
/// The platform-superadmin operations that identity-service owns (Phase 40.9). Organization CRUD
/// itself lives in organization-service, which owns the tenant registry; what lands here are the
/// two things that need identity-db — minting a token and creating an invite.
/// </summary>
public interface IPlatformAdminService
{
    /// <summary>
    /// Mints a brand-new, short-lived access token scoped to <paramref name="request"/>'s
    /// organization and writes the audit row that justifies it.
    /// </summary>
    Task<ImpersonationTokenDto> StartImpersonationAsync(
        CreateImpersonationRequestDto request,
        PlatformAdminActor actor,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ImpersonationAuditEntryDto>> ListImpersonationsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invites a <c>TenancyAdmin</c> or <c>TenancySuperAdmin</c> into an organization, reusing the
    /// Phase 40.7 invite machinery verbatim. An organization may have any number of either rank —
    /// see the implementation for why the original one-administrator cap was dropped.
    /// </summary>
    Task<BootstrapOrganizationAdminResponseDto> BootstrapOrganizationAdminAsync(
        BootstrapOrganizationAdminRequestDto request,
        PlatformAdminActor actor,
        CancellationToken cancellationToken = default);
}
