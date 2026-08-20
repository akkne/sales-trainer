using Sellevate.Identity.Features.Organizations.Models;

namespace Sellevate.Identity.Features.Organizations.Services.Abstract;

/// <summary>
/// The identity-side half of demo-request provisioning: upserts the organization replica from an
/// authoritative payload and mints (or recovers) the bootstrap administrator invite. Called only by
/// organization-service, over <c>internal/organizations/{organizationId}/bootstrap-admin</c>.
/// </summary>
public interface IOrganizationBootstrapService
{
    Task<InternalBootstrapAdministratorResponseDto> BootstrapAdministratorAsync(
        Guid organizationId,
        InternalBootstrapAdministratorRequestDto request,
        CancellationToken cancellationToken = default);
}
