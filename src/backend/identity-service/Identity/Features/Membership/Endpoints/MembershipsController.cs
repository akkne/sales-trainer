using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Identity.Common.Constants;
using Sellevate.Identity.Features.Membership.Models;
using Sellevate.Identity.Infrastructure.Data;

namespace Sellevate.Identity.Features.Membership.Endpoints;

/// <summary>
/// Offboarding for the caller's own organization.
///
/// <para>
/// <c>DELETE</c> here means <c>status = deactivated</c>, never a row deletion, and the endpoint has
/// no delete path at all — the manager's attempt history, call recordings and scores belong to the
/// organization and are exactly what the РОП pays to keep (docs/TENANCY/TENANCY.md §4.3). Erasure
/// is a separate, explicit GDPR operation that does not exist yet.
/// </para>
/// <para>
/// Removing a user from an organization is the one privilege the 2026-08-16 role split reserves
/// for a superadmin, so this route is <c>RequireOrgSuperAdmin</c> (docs/DECISIONS.md).
/// </para>
/// <para>
/// <c>Membership</c> is keyed on <c>(UserId, OrganizationId)</c> and is not <c>ITenantScoped</c>
/// (Phase 40.6), so the organization filter is written out explicitly in every query here rather
/// than inherited from a query filter.
/// </para>
/// </summary>
[ApiController]
[Route("memberships")]
[Authorize(Policy = AuthorizationPolicies.RequireOrganizationSuperAdministrator)]
[TenantScoped]
public sealed class MembershipsController(
    IdentityDbContext databaseContext,
    ITenantContext tenantContext,
    ILogger<MembershipsController> logger) : ControllerBase
{
    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> DeactivateMembership(Guid userId, CancellationToken cancellationToken)
    {
        var currentOrganizationId = tenantContext.OrganizationId;
        if (currentOrganizationId is null)
        {
            return Forbid();
        }

        var membership = await databaseContext.Memberships
            .FirstOrDefaultAsync(
                candidate => candidate.UserId == userId && candidate.OrganizationId == currentOrganizationId,
                cancellationToken);

        if (membership is null)
        {
            return NotFound();
        }

        if (membership.Status == MembershipStatus.Deactivated)
        {
            return NoContent();
        }

        membership.Status = MembershipStatus.Deactivated;
        membership.DeactivatedAt = DateTime.UtcNow;
        await databaseContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Membership deactivated TargetUserId={TargetUserId} OrganizationId={OrganizationIdentifier} ActorId={ActorId}",
            userId, currentOrganizationId, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return NoContent();
    }
}
