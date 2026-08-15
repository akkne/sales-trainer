using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.BuildingBlocks.Tenancy;
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
/// </summary>
[ApiController]
[Route("invites")]
[Authorize(Policy = "RequireOrgAdmin")]
[TenantScoped]
public sealed class InvitesController(IInviteService inviteService) : ControllerBase
{
    [HttpPost]
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
    public async Task<IActionResult> RevokeInvite(Guid inviteId, CancellationToken cancellationToken)
    {
        var wasRevoked = await inviteService.RevokeAsync(inviteId, cancellationToken);
        return wasRevoked ? NoContent() : NotFound();
    }
}
