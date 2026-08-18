using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.Identity.Common.Constants;
using Sellevate.Identity.Features.PlatformAdmin.Constants;
using Sellevate.Identity.Features.PlatformAdmin.Exceptions;
using Sellevate.Identity.Features.PlatformAdmin.Models;
using Sellevate.Identity.Features.PlatformAdmin.Services.Abstract;

namespace Sellevate.Identity.Features.PlatformAdmin.Endpoints;

/// <summary>
/// Platform-superadmin operations that need identity-db (Phase 40.9). The tenant registry itself —
/// create / list / suspend / reactivate an organization — stays in organization-service, which
/// owns it; see docs/DECISIONS.md (2026-08-15).
///
/// <para>
/// Platform-scoped, therefore deliberately **not** <c>[TenantScoped]</c>: these routes act *on*
/// organizations rather than *inside* one, so requiring an <c>X-Organization-Id</c> header would
/// be meaningless — the superadmin has no membership in the organization they are administering.
/// The organization is named in the request body instead, which is the single exception
/// docs/TENANCY/TENANCY.md §1.3 spells out, is confined to <c>RequireSuperAdmin</c> routes, and is
/// allow-listed by name in <c>scripts/tenancy-boundary-lint.py</c>.
/// </para>
/// </summary>
[ApiController]
[Route("admin/platform")]
[Authorize(Policy = AuthorizationPolicies.RequireSuperAdministrator)]
public sealed class PlatformAdminController(IPlatformAdminService platformAdminService) : ControllerBase
{
    /// <summary>
    /// Issues a brand-new short-lived token for another organization and records why. Never a
    /// parameter on an ordinary route — the whole point is that crossing a tenant boundary is a
    /// separate, auditable act (docs/TENANCY/TENANCY.md §1.3).
    /// </summary>
    [HttpPost("impersonation")]
    public async Task<ActionResult<ImpersonationTokenDto>> StartImpersonation(
        [FromBody] CreateImpersonationRequestDto request,
        CancellationToken cancellationToken)
    {
        if (ResolveActor() is not { } actor)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await platformAdminService.StartImpersonationAsync(request, actor, cancellationToken));
        }
        catch (PlatformAdminOperationException exception)
        {
            return ToErrorResult(exception);
        }
    }

    [HttpGet("impersonation")]
    public async Task<ActionResult<IReadOnlyList<ImpersonationAuditEntryDto>>> ListImpersonations(
        CancellationToken cancellationToken)
        => Ok(await platformAdminService.ListImpersonationsAsync(cancellationToken));

    /// <summary>
    /// Invites the first <c>TenancySuperAdmin</c> of a new organization, reusing the Phase 40.7 invite
    /// machinery rather than adding a second way to create a membership.
    /// </summary>
    [HttpPost("organizations/bootstrap-admin")]
    public async Task<ActionResult<BootstrapOrganizationAdminResponseDto>> BootstrapOrganizationAdmin(
        [FromBody] BootstrapOrganizationAdminRequestDto request,
        CancellationToken cancellationToken)
    {
        if (ResolveActor() is not { } actor)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await platformAdminService.BootstrapOrganizationAdminAsync(request, actor, cancellationToken));
        }
        catch (PlatformAdminOperationException exception)
        {
            return ToErrorResult(exception);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    private PlatformAdminActor? ResolveActor()
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorUserId))
        {
            return null;
        }

        return new PlatformAdminActor(
            actorUserId,
            User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            User.FindFirstValue("displayName") ?? string.Empty,
            IsAlreadyImpersonating: User.HasClaim(
                claim => claim.Type == ImpersonationClaimNames.IsImpersonation));
    }

    private ObjectResult ToErrorResult(PlatformAdminOperationException exception) => exception.Reason switch
    {
        PlatformAdminRejectionReason.OrganizationNotKnown =>
            NotFound(new { message = exception.Message }),
        PlatformAdminRejectionReason.OrganizationAlreadyBootstrapped =>
            Conflict(new { message = exception.Message }),
        _ => StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message }),
    };
}
