using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.Organization.Common.Constants;
using Sellevate.Organization.Features.Organizations.Exceptions;
using Sellevate.Organization.Features.Organizations.Models;
using Sellevate.Organization.Features.Organizations.Services.Abstract;

namespace Sellevate.Organization.Features.Organizations.Endpoints;

/// <summary>
/// Manages the tenant registry (create/list/get/update/suspend/reactivate an organization).
/// Not tenant-scoped: these routes are for the platform to administer organizations, not for an
/// organization to act on itself, so they carry no <c>[TenantScoped]</c> gate. Phase 40.9 closes
/// the placeholder that used to let any authenticated user in — the whole controller is now
/// <c>RequireSuperAdmin</c>, which is also why addressing an organization by a route id is
/// legitimate here and nowhere else (docs/TENANCY/TENANCY.md §1.3, docs/DECISIONS.md).
/// </summary>
[ApiController]
[Route(RouteConstants.OrganizationsBase)]
[Authorize(Policy = AuthorizationPolicies.RequirePlatformAdministrator)]
public sealed class OrganizationController(IOrganizationService organizationService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<OrganizationDetailDto>> CreateOrganization(
        [FromBody] CreateOrganizationRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var organization = await organizationService.CreateOrganizationAsync(request, cancellationToken);
            return Created($"/{RouteConstants.OrganizationsBase}/{organization.Id}", organization);
        }
        catch (OrganizationSlugConflictException conflictException)
        {
            return Conflict(new { code = OrganizationSlugConflictException.Code, message = conflictException.Message });
        }
        catch (ArgumentException argumentException)
        {
            return BadRequest(new { message = argumentException.Message });
        }
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrganizationSummaryDto>>> ListOrganizations(CancellationToken cancellationToken)
    {
        var organizations = await organizationService.ListOrganizationsAsync(cancellationToken);
        return Ok(organizations);
    }

    [HttpGet(RouteConstants.OrganizationById)]
    public async Task<ActionResult<OrganizationDetailDto>> GetOrganization(Guid id, CancellationToken cancellationToken)
    {
        var organization = await organizationService.GetOrganizationAsync(id, cancellationToken);
        return organization is null ? NotFound() : Ok(organization);
    }

    [HttpPut(RouteConstants.OrganizationById)]
    public async Task<ActionResult<OrganizationDetailDto>> UpdateOrganization(
        Guid id,
        [FromBody] UpdateOrganizationRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var organization = await organizationService.UpdateOrganizationAsync(id, request, cancellationToken);
            return organization is null ? NotFound() : Ok(organization);
        }
        catch (OrganizationSlugConflictException conflictException)
        {
            return Conflict(new { code = OrganizationSlugConflictException.Code, message = conflictException.Message });
        }
        catch (ArgumentException argumentException)
        {
            return BadRequest(new { message = argumentException.Message });
        }
    }

    [HttpPost(RouteConstants.SuspendOrganization)]
    public async Task<ActionResult<OrganizationDetailDto>> SuspendOrganization(Guid id, CancellationToken cancellationToken)
    {
        var organization = await organizationService.SuspendOrganizationAsync(id, cancellationToken);
        return organization is null ? NotFound() : Ok(organization);
    }

    [HttpPost(RouteConstants.ReactivateOrganization)]
    public async Task<ActionResult<OrganizationDetailDto>> ReactivateOrganization(Guid id, CancellationToken cancellationToken)
    {
        var organization = await organizationService.ReactivateOrganizationAsync(id, cancellationToken);
        return organization is null ? NotFound() : Ok(organization);
    }
}
