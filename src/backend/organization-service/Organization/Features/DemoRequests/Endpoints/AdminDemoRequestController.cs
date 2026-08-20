using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.Organization.Common.Constants;
using Sellevate.Organization.Features.DemoRequests.Constants;
using Sellevate.Organization.Features.DemoRequests.Exceptions;
using Sellevate.Organization.Features.DemoRequests.Models;
using Sellevate.Organization.Features.DemoRequests.Services.Abstract;
using Sellevate.Organization.Features.Organizations.Exceptions;

namespace Sellevate.Organization.Features.DemoRequests.Endpoints;

/// <summary>
/// The platform-staff surface over submitted demo requests: the list a sales rep works from, the
/// status update that records what happened after they reached out, and provisioning — creating the
/// organization and the bootstrap admin invite in one call.
///
/// <para>
/// <b>Provisioning is deliberately superadmin-only while the list and the status update stay
/// platform-admin.</b> Provisioning creates a membership, and per <c>AuthorizationPolicies</c> "the
/// only privilege that separates an admin from a superadmin is adding and removing users" — so this
/// controller carries two gates rather than one, with the tighter one on the one action that actually
/// adds a user. <c>[Authorize]</c> on an action is ANDed with the controller's, so the looser
/// class-level policy can never widen this one action back down.
/// </para>
/// </summary>
[ApiController]
[Route(RouteConstants.AdminDemoRequestsBase)]
[Authorize(Policy = AuthorizationPolicies.RequirePlatformAdministrator)]
public sealed class AdminDemoRequestController(
    IDemoRequestService demoRequestService,
    IDemoRequestProvisioningService demoRequestProvisioningService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DemoRequestDto>>> ListDemoRequests(
        CancellationToken cancellationToken)
    {
        var demoRequests = await demoRequestService.ListDemoRequestsAsync(cancellationToken);
        return Ok(demoRequests);
    }

    [HttpPatch(RouteConstants.AdminDemoRequestStatus)]
    public async Task<ActionResult<DemoRequestDto>> UpdateDemoRequestStatus(
        Guid id,
        [FromBody] UpdateDemoRequestStatusRequestDto request,
        CancellationToken cancellationToken)
    {
        var demoRequest = await demoRequestService.UpdateStatusAsync(id, request.Status, cancellationToken);
        return demoRequest is null ? NotFound() : Ok(demoRequest);
    }

    /// <summary>
    /// Creates the organization and sends the bootstrap invite to its first administrator, in one
    /// call. Answers <c>200</c> — never a fresh error — on a lead that was already fully provisioned,
    /// because this is a UI button and a double-click must not look like a failure.
    /// </summary>
    [HttpPost(RouteConstants.AdminDemoRequestProvision)]
    [Authorize(Policy = AuthorizationPolicies.RequireSuperAdministrator)]
    public async Task<ActionResult<DemoRequestProvisioningResultDto>> ProvisionDemoRequest(
        Guid id,
        [FromBody] ProvisionDemoRequestRequestDto? request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorUserId))
        {
            return Unauthorized();
        }

        try
        {
            var result = await demoRequestProvisioningService.ProvisionAsync(
                id,
                request ?? new ProvisionDemoRequestRequestDto(null, null, null, null),
                actorUserId,
                cancellationToken);

            return result is null ? NotFound() : Ok(result);
        }
        catch (OrganizationSlugConflictException conflictException)
        {
            return Conflict(new
            {
                code = DemoRequestProvisioningConstants.SlugTakenCode,
                slug = conflictException.Slug,
                message = conflictException.Message,
            });
        }
        catch (DemoRequestOrganizationHasAdminException organizationHasAdminException)
        {
            return Conflict(new
            {
                code = DemoRequestProvisioningConstants.OrganizationHasAdminCode,
                organizationId = organizationHasAdminException.OrganizationId,
            });
        }
        catch (DemoRequestInviteFailedException inviteFailedException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                code = DemoRequestProvisioningConstants.InviteFailedCode,
                organizationId = inviteFailedException.OrganizationId,
                provisioningState = nameof(DemoRequestProvisioningState.OrganizationCreated),
            });
        }
        catch (ArgumentException argumentException)
        {
            return BadRequest(new { message = argumentException.Message });
        }
    }
}
