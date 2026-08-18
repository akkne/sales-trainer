using Sellevate.Organization.Features.Organizations.Models;

namespace Sellevate.Organization.Features.Organizations.Services.Abstract;

/// <summary>
/// Manages the tenant registry itself (docs/TENANCY/TENANCY.md §1.2). Every method here
/// operates across organizations by design — this is the one service in the platform that is
/// legitimately allowed to address an organization by an id supplied in a route, because it IS
/// the registry, not a tenant-scoped consumer of it.
/// </summary>
public interface IOrganizationService
{
    Task<OrganizationDetailDto> CreateOrganizationAsync(
        CreateOrganizationRequestDto request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrganizationSummaryDto>> ListOrganizationsAsync(CancellationToken cancellationToken = default);

    Task<OrganizationDetailDto?> GetOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);

    Task<OrganizationDetailDto?> UpdateOrganizationAsync(
        Guid organizationId, UpdateOrganizationRequestDto request, CancellationToken cancellationToken = default);

    Task<OrganizationDetailDto?> SuspendOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);

    Task<OrganizationDetailDto?> ReactivateOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);
}
