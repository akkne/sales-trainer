using Microsoft.AspNetCore.Mvc;
using Sellevate.Identity.Common.Security;
using Sellevate.Identity.Features.Organizations.Exceptions;
using Sellevate.Identity.Features.Organizations.Models;
using Sellevate.Identity.Features.Organizations.Services.Abstract;

namespace Sellevate.Identity.Features.Organizations.Endpoints;

/// <summary>
/// Demo-request provisioning's one call into identity-service: upsert the organization replica and
/// mint (or recover) its first administrator's invite, in one request. See
/// <c>OrganizationBootstrapService</c> for why the replica upsert cannot wait for Kafka here, and
/// docs/DECISIONS.md for why this is a synchronous cross-service call at all.
///
/// <para>
/// <b>Guarded by the shared internal-service secret, not <c>[Authorize]</c>, and deliberately not
/// <c>[TenantScoped]</c>.</b> The caller is organization-service itself, which carries no JWT and no
/// <c>X-Organization-Id</c> header for a tenant it has no membership in — the organization is named in
/// the route instead, the same carve-out <c>PlatformAdminController</c> already relies on
/// (docs/TENANCY/TENANCY.md §1.3), allow-listed by this file's path in
/// <c>scripts/tenancy-boundary-lint.py</c>.
/// </para>
/// </summary>
[ApiController]
[Route("internal/organizations/{organizationId:guid}/bootstrap-admin")]
[ServiceFilter(typeof(InternalServiceAuthFilter))]
public sealed class InternalOrganizationBootstrapController(IOrganizationBootstrapService organizationBootstrapService)
    : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<InternalBootstrapAdministratorResponseDto>> BootstrapAdministrator(
        Guid organizationId,
        [FromBody] InternalBootstrapAdministratorRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await organizationBootstrapService.BootstrapAdministratorAsync(organizationId, request, cancellationToken));
        }
        catch (OrganizationBootstrapOperationException operationException)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = operationException.Message });
        }
        catch (ArgumentException argumentException)
        {
            return BadRequest(new { message = argumentException.Message });
        }
    }
}
