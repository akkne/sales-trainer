using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.TeamInsights.Models;
using Sellevate.Learning.Features.TeamInsights.Services.Abstract;

namespace Sellevate.Learning.Features.TeamInsights;

/// <summary>
/// Phase 40.25. The team-level half of the РОП's dashboard: the skill heat map, and where each
/// manager sags, named by the sales-funnel stage (docs/TENANCY/ASSIGNMENTS.md §4).
///
/// <para>
/// One gate — <see cref="AuthorizationPolicies.RequireOrganizationAdministrator"/> — for the same
/// reason <c>AdminAssignmentsController</c> has one: every row here is strict tenant data, there is
/// no global counterpart to protect, and the organization is never read from the request. It comes
/// from <see cref="ITenantContext"/> (docs/TENANCY/TENANCY.md §1.3).
/// </para>
/// </summary>
[ApiController]
[TenantScoped]
[Authorize(Policy = AuthorizationPolicies.RequireOrganizationAdministrator)]
public sealed class AdminTeamInsightsController(ITeamSkillMapService teamSkillMapService) : ControllerBase
{
    [HttpGet("admin/team/skill-map")]
    public async Task<ActionResult<TeamSkillMapDto>> GetSkillMap(
        [FromQuery] int days = 0,
        CancellationToken cancellationToken = default)
        => Ok(await teamSkillMapService.GetSkillMapAsync(days, cancellationToken));
}
