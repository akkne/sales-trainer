using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Common.Extensions;
using Sellevate.Learning.Features.Content.Models;
using Sellevate.Learning.Features.Content.Services.Abstract;

namespace Sellevate.Learning.Features.Admin;

/// <summary>
/// Phase 40.18. Copy-on-write overrides and the staleness review queue, for an organization's own
/// administrator (docs/TENANCY/CONTENT_MODEL.md §2.6).
///
/// <para>
/// <b>Every route here is an act, not a schedule.</b> There is no onboarding hook, no consumer and
/// no background sweep that creates a copy: <c>POST .../{kind}/{baseId}</c> is reachable only from a
/// person pressing "edit". That is the whole design — an organization that gets a forked library on
/// day one stops receiving improvements to the base for ever, and nobody notices until the content
/// roadmap has quietly stopped existing.
/// </para>
///
/// <para>
/// The organization comes from <c>ITenantContext</c> and is never read from the body, the query
/// string or the route — the boundary lint enforces that, and the two review actions would
/// otherwise be a way to retire somebody else's override by guessing an id.
/// </para>
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.RequireOrganizationAdministrator)]
public sealed class AdminContentOverridesController(
    IContentOverrideService contentOverrideService) : ControllerBase
{
    /// <summary>
    /// The organization's overrides. <c>?staleOnly=true</c> is the review queue: the overrides whose
    /// base has published something newer since they were forked or last reviewed.
    /// </summary>
    [HttpGet("admin/content/overrides")]
    public async Task<ActionResult<IReadOnlyList<ContentOverrideDto>>> GetOverrides(
        [FromQuery] bool staleOnly = false,
        CancellationToken cancellationToken = default)
        => Ok(await contentOverrideService.GetOverridesAsync(staleOnly, cancellationToken));

    /// <summary>What changed upstream, what the organization changed. Nothing pre-merged.</summary>
    [HttpGet("admin/content/overrides/{kind}/{overrideId:guid}")]
    public async Task<ActionResult<ContentOverrideReviewDto>> GetReview(
        string kind, Guid overrideId, CancellationToken cancellationToken = default)
    {
        if (!ContentOverrideKinds.IsKnown(kind)) return NotFound();

        var review = await contentOverrideService.GetReviewAsync(kind, overrideId, cancellationToken);

        return review is null ? NotFound() : Ok(review);
    }

    /// <summary>Copy-on-write. Idempotent: a second press returns the existing copy untouched.</summary>
    [HttpPost("admin/content/overrides/{kind}/{baseId:guid}")]
    public async Task<ActionResult<ContentOverrideDto>> CreateOverride(
        string kind, Guid baseId, CancellationToken cancellationToken = default)
    {
        if (!ContentOverrideKinds.IsKnown(kind)) return NotFound();

        User.TryResolveUserId(out var actorId);

        var result = await contentOverrideService.CreateOverrideAsync(kind, baseId, actorId, cancellationToken);

        return result.Outcome switch
        {
            ContentOverrideOutcome.Created => Ok(result.Override),
            ContentOverrideOutcome.AlreadyExists => Ok(result.Override),
            ContentOverrideOutcome.SourceNotFound => NotFound(),
            ContentOverrideOutcome.SourceNotGlobal => Conflict(new
            {
                message = "That row is already an organization's own copy, not part of the global library.",
            }),
            ContentOverrideOutcome.NoOrganization => BadRequest(new
            {
                message = "An override belongs to an organization, and this request is not inside one.",
            }),
            _ => NotFound(),
        };
    }

    /// <summary>Review action one: take the new base, discarding the override.</summary>
    [HttpPost("admin/content/overrides/{kind}/{overrideId:guid}/accept-base")]
    public async Task<IActionResult> AcceptBase(
        string kind, Guid overrideId, CancellationToken cancellationToken = default)
    {
        if (!ContentOverrideKinds.IsKnown(kind)) return NotFound();

        User.TryResolveUserId(out var actorId);

        return await contentOverrideService.AcceptBaseAsync(kind, overrideId, actorId, cancellationToken)
            ? NoContent()
            : NotFound();
    }

    /// <summary>
    /// Review action two: keep the override, re-pointing its fork marker at the base as it is now.
    /// The override's own content is untouched — this records a decision, it does not merge.
    /// </summary>
    [HttpPost("admin/content/overrides/{kind}/{overrideId:guid}/keep-override")]
    public async Task<IActionResult> KeepOverride(
        string kind, Guid overrideId, CancellationToken cancellationToken = default)
    {
        if (!ContentOverrideKinds.IsKnown(kind)) return NotFound();

        User.TryResolveUserId(out var actorId);

        return await contentOverrideService.KeepOverrideAsync(kind, overrideId, actorId, cancellationToken)
            ? NoContent()
            : NotFound();
    }
}
