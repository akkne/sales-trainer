using Sellevate.Organization.Features.Organizations.Models;

namespace Sellevate.Organization.Features.Organizations.Services.Abstract;

/// <summary>
/// Reads and writes the caller's own organization profile. Unlike <see cref="IOrganizationService"/>,
/// there is no organization-id parameter anywhere on this interface: the target organization
/// comes solely from the scoped <c>ITenantContext</c>, which the caller cannot override
/// (docs/TENANCY/TENANCY.md §1.3).
/// </summary>
public interface IOrganizationProfileService
{
    Task<OrganizationProfileDto?> GetProfileAsync(CancellationToken cancellationToken = default);

    Task<OrganizationProfileDto> UpsertProfileAsync(
        UpdateOrganizationProfileRequestDto request, CancellationToken cancellationToken = default);
}
