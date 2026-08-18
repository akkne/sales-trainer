using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.Ai.Common.Constants;
using Sellevate.Ai.Features.Dialog.Models;
using Sellevate.Ai.Infrastructure.Data;

namespace Sellevate.Ai.Features.Dialog.Overrides;

/// <summary>
/// Phase 40.18. Per-organization prompt overrides and their staleness queue, for an organization's
/// own administrator (docs/TENANCY/CONTENT_MODEL.md §2.6, §4).
///
/// <para>
/// A copy is made only here, only by <c>POST</c>, and only because a person pressed "edit". Nothing
/// creates one at onboarding: an organization handed a private fork of the prompt library on day one
/// stops receiving every later improvement to it, permanently and invisibly.
/// </para>
///
/// <para>
/// The organization comes from <c>ITenantContext</c>, never from the route, the body or the query
/// string. The controller carries <see cref="TenantTransactionAttribute"/> because without a
/// transaction <c>SET LOCAL app.organization_id</c> never runs, and an organization's own override
/// is invisible to the very screen that manages it.
/// </para>
/// </summary>
[ApiController]
[Route("admin/dialog/overrides")]
[TenantTransaction]
[Authorize(Policy = AuthorizationPolicies.RequireOrganizationAdministrator)]
public sealed class AdminDialogOverridesController(
    IDialogModeOverrideService overrideService) : ControllerBase
{
    /// <summary><c>?staleOnly=true</c> is the review queue.</summary>
    [HttpGet("modes")]
    public async Task<ActionResult<IReadOnlyList<DialogModeOverrideDto>>> GetOverrides(
        [FromQuery] bool staleOnly = false,
        CancellationToken cancellationToken = default)
        => Ok(await overrideService.GetOverridesAsync(staleOnly, cancellationToken));

    /// <summary>Both prompts, side by side, unmerged.</summary>
    [HttpGet("modes/{overrideId:guid}")]
    public async Task<ActionResult<DialogModeOverrideReviewDto>> GetReview(
        Guid overrideId, CancellationToken cancellationToken = default)
    {
        var review = await overrideService.GetReviewAsync(overrideId, cancellationToken);

        return review is null ? NotFound() : Ok(review);
    }

    /// <summary>Copy-on-write. Idempotent: a second press returns the existing copy untouched.</summary>
    [HttpPost("modes/{baseModeId:guid}")]
    public async Task<ActionResult<DialogModeOverrideDto>> CreateOverride(
        Guid baseModeId, CancellationToken cancellationToken = default)
    {
        var result = await overrideService.CreateOverrideAsync(baseModeId, cancellationToken);

        return result.Outcome switch
        {
            DialogModeOverrideOutcome.Created => Ok(result.Override),
            DialogModeOverrideOutcome.AlreadyExists => Ok(result.Override),
            DialogModeOverrideOutcome.SourceNotFound => NotFound(),
            DialogModeOverrideOutcome.SourceNotGlobal => Conflict(new
            {
                message = "That mode is already an organization's own copy, not part of the global library.",
            }),
            DialogModeOverrideOutcome.SourceIsSeededHiddenMode => Conflict(new
            {
                message = "The seeded hidden modes stay global: their prompts are completed by the service at run time.",
            }),
            DialogModeOverrideOutcome.NoOrganization => BadRequest(new
            {
                message = "An override belongs to an organization, and this request is not inside one.",
            }),
            _ => NotFound(),
        };
    }

    /// <summary>
    /// Review action three: edit the organization's own prompt. Same partial-update shape as
    /// <c>PUT /admin/dialog/modes/:modeId</c>, but reachable by an organization administrator and
    /// only ever aimed at a row they own. `key` and `bundleId` are not editable — they are the
    /// override's link to the row it shadows.
    /// </summary>
    [HttpPut("modes/{overrideId:guid}")]
    public async Task<ActionResult<AdminDialogModeDto>> UpdateOverride(
        Guid overrideId, [FromBody] UpdateModeRequestDto request, CancellationToken cancellationToken = default)
    {
        var updated = await overrideService.UpdateOverrideAsync(overrideId, request, cancellationToken);

        return updated is null ? NotFound() : Ok(AdminDialogModeDto.FromEntity(updated));
    }

    /// <summary>Review action one: take the new base prompt, retiring the override.</summary>
    [HttpPost("modes/{overrideId:guid}/accept-base")]
    public async Task<IActionResult> AcceptBase(Guid overrideId, CancellationToken cancellationToken = default)
        => await overrideService.AcceptBaseAsync(overrideId, cancellationToken) ? NoContent() : NotFound();

    /// <summary>
    /// Review action two: keep the override, re-pointing its fork marker at the base as it is now.
    /// The prompt is not touched — this records a decision.
    /// </summary>
    [HttpPost("modes/{overrideId:guid}/keep-override")]
    public async Task<IActionResult> KeepOverride(Guid overrideId, CancellationToken cancellationToken = default)
        => await overrideService.KeepOverrideAsync(overrideId, cancellationToken) ? NoContent() : NotFound();
}
