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
/// for a superadmin, so <see cref="DeactivateMembership"/> carries <c>RequireOrgSuperAdmin</c> on
/// top of the controller's <c>RequireOrgAdmin</c> (attributes are ANDed, and every superadmin
/// satisfies the admin policy). Reading the roster is not that privilege: a <c>TenancyAdmin</c>
/// hands out assignments to these people and cannot do it blind.
/// </para>
/// <para>
/// <c>Membership</c> is keyed on <c>(UserId, OrganizationId)</c> and is not <c>ITenantScoped</c>
/// (Phase 40.6), so the organization filter is written out explicitly in every query here rather
/// than inherited from a query filter.
/// </para>
/// </summary>
[ApiController]
[Route("memberships")]
[Authorize(Policy = AuthorizationPolicies.RequireOrganizationAdministrator)]
[TenantScoped]
public sealed class MembershipsController(
    IdentityDbContext databaseContext,
    ITenantContext tenantContext,
    ILogger<MembershipsController> logger) : ControllerBase
{
    /// <summary>
    /// The organization's roster. Defaults to the people who still work here; ask for
    /// <c>deactivated</c> or <c>all</c> to see the ones who no longer do.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MembershipDto>>> ListMemberships(
        [FromQuery] MembershipStatusFilter status = MembershipStatusFilter.Active,
        CancellationToken cancellationToken = default)
    {
        var currentOrganizationId = tenantContext.OrganizationId;
        if (currentOrganizationId is null)
        {
            return Forbid();
        }

        var memberships = databaseContext.Memberships
            .Where(candidate => candidate.OrganizationId == currentOrganizationId);

        memberships = status switch
        {
            MembershipStatusFilter.Active =>
                memberships.Where(candidate => candidate.Status == MembershipStatus.Active),
            MembershipStatusFilter.Deactivated =>
                memberships.Where(candidate => candidate.Status == MembershipStatus.Deactivated),
            _ => memberships,
        };

        var roster = await memberships
            .Join(
                databaseContext.Users,
                membership => membership.UserId,
                user => user.Id,
                (membership, user) => new { membership, user })
            .OrderBy(row => row.user.DisplayName)
            .Select(row => new MembershipDto(
                row.membership.UserId,
                row.user.Email,
                row.user.DisplayName,
                row.membership.Role.ToString(),
                row.membership.Status.ToString(),
                row.membership.JoinedAt,
                row.membership.DeactivatedAt))
            .ToListAsync(cancellationToken);

        return Ok(roster);
    }

    [HttpDelete("{userId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.RequireOrganizationSuperAdministrator)]
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
