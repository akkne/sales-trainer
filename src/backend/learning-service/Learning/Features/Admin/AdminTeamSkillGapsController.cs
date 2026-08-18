using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Common.Extensions;
using Sellevate.Learning.Features.Content;
using Sellevate.Learning.Features.ContentGeneration.Models;
using Sellevate.Learning.Features.TeamInsights.Models;
using Sellevate.Learning.Features.TeamInsights.Services.Abstract;

namespace Sellevate.Learning.Features.Admin;

/// <summary>
/// Phase 40.31. The loop from metric to content, as four verbs (roadmap 40.31).
///
/// <para>
/// <b>The block's whole claim is in the second one.</b> A report gets opened once a quarter; a tool
/// that names the next action gets opened weekly. <c>GET /admin/team/skill-map</c> (40.25) is the
/// report — it says where the team sags. This controller is what makes the answer actionable:
/// «команда валит закрытие — сгенерировать упражнения на это?» and one press.
/// </para>
///
/// <para>
/// <b>The two remaining verbs exist so the first two stay worth reading.</b> A panel that proposes
/// the same thing every Monday is a panel the РОП learns to scroll past, and then the week it
/// matters it is already invisible. So «не сейчас» is recorded, and taking it back is a route
/// rather than a support ticket.
/// </para>
///
/// <para>
/// <b>The organization is never in a route or a body</b>, here as everywhere: it comes from
/// <c>ITenantContext</c> (docs/TENANCY/TENANCY.md §1.3). The stage key in the path is the funnel
/// stage, which is platform vocabulary and not a tenant identifier — and it is checked against the
/// measurement before anything happens, so naming a stage the team is not failing gets a 409 rather
/// than a run.
/// </para>
///
/// <para>
/// <b><c>[TenantTransaction]</c> is load-bearing.</b> Two of the four routes write, and
/// <c>SET LOCAL app.organization_id</c> does nothing outside a transaction — the same reason
/// <c>AdminContentGenerationController</c> carries it.
/// </para>
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.RequireOrganizationAdministrator)]
[TenantTransaction]
public sealed class AdminTeamSkillGapsController(ITeamSkillGapService teamSkillGapService) : ControllerBase
{
    /// <summary>
    /// What the dashboard proposes doing next, over the same window
    /// <c>GET /admin/team/skill-map?days=</c> was drawn over.
    /// </summary>
    [HttpGet("admin/team/skill-gaps")]
    public async Task<ActionResult<TeamSkillGapsDto>> GetGaps(
        [FromQuery] int days = 0,
        CancellationToken cancellationToken = default)
        => Ok(await teamSkillGapService.GetGapsAsync(days, cancellationToken));

    /// <summary>
    /// The button. Starts a 40.27 pipeline run aimed at one failing stage, with the material composed
    /// from the measurement and the organization profile rather than from an upload.
    ///
    /// <para>
    /// It stops at the same checkpoint every other run stops at, and the lesson it eventually
    /// produces arrives archived — the loop this block closes ends at reviewable content, never at
    /// unreviewed model output in the team's live tree.
    /// </para>
    /// </summary>
    [HttpPost("admin/team/skill-gaps/{stageKey}/content")]
    public async Task<ActionResult<ContentGenerationJobDto>> StartContent(
        string stageKey,
        CancellationToken cancellationToken = default)
    {
        User.TryResolveUserId(out var actorId);

        try
        {
            return Ok(await teamSkillGapService.StartContentAsync(stageKey, actorId, cancellationToken));
        }
        catch (TeamSkillGapStateException stateException)
        {
            return Conflict(new { message = stateException.Message });
        }
        catch (ContentGenerationValidationException validationException)
        {
            return BadRequest(new { message = validationException.Message });
        }
    }

    /// <summary>«Не сейчас». Returns the recomputed panel, so the caller sees the offer disappear.</summary>
    [HttpPost("admin/team/skill-gaps/{stageKey}/dismiss")]
    public async Task<ActionResult<TeamSkillGapsDto>> Dismiss(
        string stageKey,
        [FromBody] DismissTeamSkillGapRequestDto requestDto,
        CancellationToken cancellationToken = default)
    {
        User.TryResolveUserId(out var actorId);

        try
        {
            return Ok(await teamSkillGapService.DismissAsync(stageKey, requestDto, actorId, cancellationToken));
        }
        catch (TeamSkillGapStateException stateException)
        {
            return Conflict(new { message = stateException.Message });
        }
    }

    /// <summary>Takes a refusal back before it expires.</summary>
    [HttpDelete("admin/team/skill-gaps/{stageKey}/dismiss")]
    public async Task<IActionResult> Restore(
        string stageKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await teamSkillGapService.RestoreAsync(stageKey, cancellationToken)
                ? NoContent()
                : NotFound();
        }
        catch (TeamSkillGapStateException stateException)
        {
            return Conflict(new { message = stateException.Message });
        }
    }
}
