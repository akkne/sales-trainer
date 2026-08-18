using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Identity.Common.Security;
using Sellevate.Identity.Features.Membership.Models;
using Sellevate.Identity.Infrastructure.Data;

namespace Sellevate.Identity.Features.Membership.Endpoints;

/// <summary>
/// Phase 40.23. The one question another service is allowed to ask identity-service: who works here
/// right now (docs/TENANCY/ASSIGNMENTS.md §1, roadmap 40.23 "Аудитория").
///
/// <para>
/// <b>Why this endpoint exists at all, when every other cross-service read in this system is a Kafka
/// replica.</b> An assignment's audience is a rule — <c>whole_team</c>, a list, a group — and
/// resolving it produces the progress rows that <em>are</em> the record of who was asked. A replica
/// of memberships in learning-db would have been the consistent-looking choice, and it fails in the
/// one way this feature cannot survive: a replica that is behind, or that has never been backfilled,
/// resolves "the whole team" to three people out of forty and issues the assignment to three people.
/// Nothing anywhere reports an error, and the РОП's funnel is simply wrong. Asking the owner of the
/// data, once, at the moment a human presses "issue", turns that into identity-service being
/// unreachable and the issue failing loudly — which the РОП can retry. The full argument, and the
/// two rejected alternatives, are in docs/DECISIONS.md (2026-08-18).
/// </para>
///
/// <para>
/// <b>The organization comes from <see cref="ITenantContext"/>, never from the request.</b> The
/// caller is a service and carries no JWT, so the tenant arrives in the same
/// <c>X-Organization-Id</c> header the gateway would have set, and <see cref="TenantScopedAttribute"/>
/// refuses the request outright when it is absent (docs/TENANCY/TENANCY.md §1.3). What guards the
/// route itself is <see cref="InternalServiceAuthFilter"/>: without it, anybody able to address the
/// pod could name any organization and read its roster.
/// </para>
/// </summary>
[ApiController]
[Route("internal/memberships")]
[TenantScoped]
[ServiceFilter(typeof(InternalServiceAuthFilter))]
public sealed class InternalMembershipsController(
    IdentityDbContext databaseContext,
    ITenantContext tenantContext) : ControllerBase
{
    /// <summary>
    /// The user ids of every <b>active</b> membership in the calling organization.
    ///
    /// <para>
    /// Deactivated memberships are excluded, and that is the behaviour the whole feature rests on:
    /// leaving an organization is a status change rather than a row deletion (Phase 40.7), so
    /// without this filter every assignment issued after somebody's last day would still be issued
    /// to them — an email to an ex-employee about homework, and a permanent "has not started" row
    /// on the РОП's screen.
    /// </para>
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<OrganizationMemberIdsDto>> GetActiveMemberIds(
        CancellationToken cancellationToken = default)
    {
        var currentOrganizationId = tenantContext.OrganizationId;
        if (currentOrganizationId is null)
        {
            // Unreachable through [TenantScoped] for a caller with no platform role, which a
            // service never has. Kept because "no organization" must never widen into "everybody".
            return Forbid();
        }

        // Memberships is not ITenantScoped (Phase 40.6) and carries no row-level-security policy, so
        // the organization filter is written out here rather than inherited from a query filter.
        // That is the same shape MembershipsController uses one file over, and it is the reason this
        // controller reads the tenant explicitly instead of trusting an ambient filter to exist.
        var userIds = await databaseContext.Memberships
            .AsNoTracking()
            .Where(membership => membership.OrganizationId == currentOrganizationId
                                 && membership.Status == MembershipStatus.Active)
            .Select(membership => membership.UserId)
            .OrderBy(userId => userId)
            .ToListAsync(cancellationToken);

        return Ok(new OrganizationMemberIdsDto(userIds));
    }
}
