using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Identity.Common.Constants;
using Sellevate.Identity.Features.Invites.Models;
using Sellevate.Identity.Features.Invites.Services.Abstract;

namespace Sellevate.Identity.Features.Invites.Endpoints;

/// <summary>
/// Invite management for the caller's own organization.
///
/// <para>
/// The route carries no organization segment on purpose. The roadmap sketched
/// <c>/organizations/{id}/invites</c>, but the organization must come from the gateway-validated
/// <c>X-Organization-Id</c> header and never from the route (docs/TENANCY/TENANCY.md §1.3), and
/// <c>/organizations/*</c> is already owned by organization-service in the gateway. A top-level
/// <c>/invites</c> served by identity-service satisfies both — see docs/DECISIONS.md (2026-08-15).
/// <see cref="TenantScopedAttribute"/> makes <c>TenantContextMiddleware</c> answer 403 before the
/// action runs if that header is missing.
/// </para>
/// <para>
/// Inviting someone into an organization and revoking that invite are both "add/remove a user",
/// the one privilege the 2026-08-16 role split reserves for a superadmin — so both of those actions
/// carry <c>RequireOrgSuperAdmin</c> (docs/DECISIONS.md). <b>Reading</b> the list is not that
/// privilege, so the controller gate is <c>RequireOrgAdmin</c> and the two write actions add the
/// stricter policy on top; authorize attributes are ANDed, and every superadmin already satisfies
/// the admin policy.
/// </para>
/// </summary>
[ApiController]
[Route("invites")]
[Authorize(Policy = AuthorizationPolicies.RequireOrganizationAdministrator)]
[TenantScoped]
public sealed class InvitesController(IInviteService inviteService) : ControllerBase
{
    /// <summary>
    /// The organization's invites, newest first. Defaults to the pending ones — an accepted invite
    /// is a membership now and belongs on the roster instead.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InviteSummaryDto>>> ListInvites(
        [FromQuery] InviteStatusFilter status = InviteStatusFilter.Pending,
        CancellationToken cancellationToken = default)
        => Ok(await inviteService.ListAsync(status, cancellationToken));

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.RequireOrganizationSuperAdministrator)]
    public async Task<ActionResult<CreateInvitesResponseDto>> CreateInvites(
        [FromBody] CreateInvitesRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var invitedByUserId))
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await inviteService.CreateAsync(request, invitedByUserId, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpDelete("{inviteId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.RequireOrganizationSuperAdministrator)]
    public async Task<IActionResult> RevokeInvite(Guid inviteId, CancellationToken cancellationToken)
    {
        var wasRevoked = await inviteService.RevokeAsync(inviteId, cancellationToken);
        return wasRevoked ? NoContent() : NotFound();
    }
}
