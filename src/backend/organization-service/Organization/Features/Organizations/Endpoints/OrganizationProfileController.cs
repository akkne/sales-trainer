using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Organization.Common.Constants;
using Sellevate.Organization.Features.Organizations.Models;
using Sellevate.Organization.Features.Organizations.Services.Abstract;

namespace Sellevate.Organization.Features.Organizations.Endpoints;

/// <summary>
/// Reads and writes the caller's own organization profile (docs/TENANCY/CONTENT_MODEL.md §3).
/// <see cref="TenantScopedAttribute"/> makes <c>TenantContextMiddleware</c> reject any request
/// with no <c>X-Organization-Id</c> header with 403 before it reaches this controller — there is
/// no route parameter for the organization id, by design (docs/TENANCY/TENANCY.md §1.3).
/// </summary>
[ApiController]
[Route(RouteConstants.OrganizationProfileBase)]
[Authorize]
[TenantScoped]
public sealed class OrganizationProfileController(IOrganizationProfileService organizationProfileService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<OrganizationProfileDto>> GetProfile(CancellationToken cancellationToken)
    {
        var profile = await organizationProfileService.GetProfileAsync(cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPut]
    public async Task<ActionResult<OrganizationProfileDto>> UpdateProfile(
        [FromBody] UpdateOrganizationProfileRequestDto request,
        CancellationToken cancellationToken)
    {
        var profile = await organizationProfileService.UpsertProfileAsync(request, cancellationToken);
        return Ok(profile);
    }
}
