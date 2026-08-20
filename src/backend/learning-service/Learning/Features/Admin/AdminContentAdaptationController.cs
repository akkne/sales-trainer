using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Common.Extensions;
using Sellevate.Learning.Features.Content;
using Sellevate.Learning.Features.ContentAdaptation.Models;
using Sellevate.Learning.Features.ContentAdaptation.Services.Abstract;

namespace Sellevate.Learning.Features.Admin;

/// <summary>
/// Phase 40.32. «Перепиши все упражнения этапа "закрытие" под наш продукт и тон» → a background
/// batch → a list of diffs → accept or reject <b>one at a time</b> (roadmap 40.32).
///
/// <para>
/// <b>There is no bulk verb and there will not be one.</b> Not an omission and not a later feature:
/// «никогда не автоприменение» is the block, and «применить всё» is auto-apply with a person's name
/// attached to it. The only routes that write content are <c>accept</c> and <c>reject</c>, each aimed
/// at exactly one item by id. If sixty decisions is too many, the answer is a narrower stage — which
/// is what the per-batch ceiling exists to force.
/// </para>
///
/// <para>
/// <b>Two purposes, one machine.</b> <c>mode=tone_rewrite</c> proposes new wording;
/// <c>mode=quality_review</c> reports what is methodically wrong with content the РОП wrote by hand,
/// as codes rather than as prose. They share the batch, the lease, the queue and this controller
/// because they differ only in the prompt and in whether an item carries anything applicable —
/// accepting a review finding is refused with 409, and the database refuses it too.
/// </para>
///
/// <para>
/// <b>One gate, as for the pipeline and unlike lesson publishing.</b> The second gate there exists
/// because a lesson with <c>OrganizationId IS NULL</c> is the global library. A batch has no such
/// shape: its column is not nullable, and the one place it can reach the global library — accepting a
/// rewrite of a base exercise — forks the lesson through 40.18's copy-on-write instead of writing the
/// shared row.
/// </para>
///
/// <para>
/// <b><c>[TenantTransaction]</c> is load-bearing here, not decoration.</b> Everything this controller
/// reads and writes is owned by an organization, and <c>SET LOCAL app.organization_id</c> does
/// nothing outside a transaction.
/// </para>
/// </summary>
[ApiController]
[TenantScoped]
[Authorize(Policy = AuthorizationPolicies.RequireOrganizationAdministrator)]
[TenantTransaction]
public sealed class AdminContentAdaptationController(
    IContentAdaptationJobService contentAdaptationJobService) : ControllerBase
{
    [HttpGet("admin/content/adaptations")]
    public async Task<ActionResult<IReadOnlyList<ContentAdaptationJobSummaryDto>>> GetJobs(
        [FromQuery] string? mode = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await contentAdaptationJobService.GetJobsAsync(mode, status, cancellationToken));
        }
        catch (ContentAdaptationValidationException validationException)
        {
            return BadRequest(new { message = validationException.Message });
        }
    }

    /// <summary>
    /// The review screen's payload: the batch and its queue. Polled while the status is
    /// <c>preparing</c> — a stage's worth of calls takes minutes and holds no HTTP connection open.
    /// </summary>
    [HttpGet("admin/content/adaptations/{jobId:guid}")]
    public async Task<ActionResult<ContentAdaptationJobDto>> GetJob(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await contentAdaptationJobService.GetJobAsync(jobId, cancellationToken);

        return job is null ? NotFound() : Ok(job);
    }

    /// <summary>
    /// One item with both documents and the field-level change list. <b>The diff, as a person reads
    /// it</b>: the current body, the proposed body, which leaves moved, and the model's own sentence
    /// about why. Nothing is merged — that is 40.18's rule and it holds here.
    /// </summary>
    [HttpGet("admin/content/adaptations/{jobId:guid}/items/{itemId:guid}")]
    public async Task<ActionResult<ContentAdaptationItemDto>> GetItem(
        Guid jobId,
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        var item = await contentAdaptationJobService.GetItemAsync(jobId, itemId, cancellationToken);

        return item is null ? NotFound() : Ok(item);
    }

    /// <summary>
    /// Starts a batch. Spends nothing: the scope is one database query, and every LLM call happens
    /// later in the worker, one exercise at a time.
    /// </summary>
    [HttpPost("admin/content/adaptations")]
    public async Task<ActionResult<ContentAdaptationJobDto>> Start(
        [FromBody] StartContentAdaptationRequestDto requestDto,
        CancellationToken cancellationToken = default)
    {
        User.TryResolveUserId(out var actorId);

        try
        {
            var job = await contentAdaptationJobService.StartAsync(requestDto, actorId, cancellationToken);

            return CreatedAtAction(nameof(GetJob), new { jobId = job.Summary.Id }, job);
        }
        catch (ContentAdaptationValidationException validationException)
        {
            return BadRequest(new { message = validationException.Message });
        }
        catch (ContentAdaptationStateException stateException)
        {
            return Conflict(new { message = stateException.Message });
        }
    }

    /// <summary>
    /// «Применить.» The only route in the block that writes an exercise, and it needs an item id — so
    /// the smallest thing that can be applied is one exercise, and applying it took a human click.
    /// 409 when the exercise has moved since the proposal was computed: the answer to a stale
    /// proposal is a re-run, never a merge.
    /// </summary>
    [HttpPost("admin/content/adaptations/{jobId:guid}/items/{itemId:guid}/accept")]
    public async Task<ActionResult<ContentAdaptationItemDto>> AcceptItem(
        Guid jobId,
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        User.TryResolveUserId(out var actorId);

        try
        {
            var item = await contentAdaptationJobService.AcceptItemAsync(
                jobId, itemId, actorId, cancellationToken);

            return item is null ? NotFound() : Ok(item);
        }
        catch (ContentAdaptationStateException stateException)
        {
            return Conflict(new { message = stateException.Message });
        }
    }

    /// <summary>
    /// «Нет.» Touches no content and remembers nothing beyond the item row — a rejected rewrite is not
    /// a standing exemption, because the next batch is a new question asked of a possibly different
    /// profile.
    /// </summary>
    [HttpPost("admin/content/adaptations/{jobId:guid}/items/{itemId:guid}/reject")]
    public async Task<ActionResult<ContentAdaptationItemDto>> RejectItem(
        Guid jobId,
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        User.TryResolveUserId(out var actorId);

        try
        {
            var item = await contentAdaptationJobService.RejectItemAsync(
                jobId, itemId, actorId, cancellationToken);

            return item is null ? NotFound() : Ok(item);
        }
        catch (ContentAdaptationStateException stateException)
        {
            return Conflict(new { message = stateException.Message });
        }
    }

    /// <summary>Re-queues the items that burned their attempts, and only those.</summary>
    [HttpPost("admin/content/adaptations/{jobId:guid}/retry")]
    public async Task<ActionResult<ContentAdaptationJobDto>> Retry(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var job = await contentAdaptationJobService.RetryAsync(jobId, cancellationToken);

            return job is null ? NotFound() : Ok(job);
        }
        catch (ContentAdaptationStateException stateException)
        {
            return Conflict(new { message = stateException.Message });
        }
    }
}
