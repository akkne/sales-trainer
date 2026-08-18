using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Common.Extensions;
using Sellevate.Learning.Features.Content;
using Sellevate.Learning.Features.ContentGeneration.Models;
using Sellevate.Learning.Features.ContentGeneration.Services.Abstract;

namespace Sellevate.Learning.Features.Admin;

/// <summary>
/// Phase 40.27. The РОП's content pipeline, with the stop in the middle (roadmap 40.27).
///
/// <para>
/// <b>Four verbs and one of them is the block.</b> Start a run, read what was extracted, correct it,
/// approve it. Everything expensive happens on the far side of the approval, so the correction costs
/// thirty seconds and the same correction after generation costs a re-generation of fifteen
/// exercises — plus the tokens spent producing the exercises that were about to be thrown away.
/// </para>
///
/// <para>
/// <b>One gate, as for assignments and unlike lesson publishing.</b> The second gate there exists
/// because a lesson with <c>OrganizationId IS NULL</c> is the global library. A pipeline run has no
/// such shape: the column is not nullable, there is no global run, and everything it writes is owned
/// by the caller's organization. So <see cref="AuthorizationPolicies.RequireOrganizationAdministrator"/>
/// plus the tenancy layer is the whole boundary.
/// </para>
///
/// <para>
/// <b><c>[TenantTransaction]</c> is load-bearing here, not decoration.</b> Everything this controller
/// reads and writes is owned by an organization, and <c>SET LOCAL app.organization_id</c> does
/// nothing outside a transaction — without the filter the administrator would create a run and then
/// not be able to find it (docs/TENANCY/CONTENT_MODEL.md, 40.18's note on the admin controllers).
/// </para>
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.RequireOrganizationAdministrator)]
[TenantTransaction]
public sealed class AdminContentGenerationController(
    IContentGenerationJobService contentGenerationJobService) : ControllerBase
{
    [HttpGet("admin/content-generation")]
    public async Task<ActionResult<IReadOnlyList<ContentGenerationJobSummaryDto>>> GetJobs(
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await contentGenerationJobService.GetJobsAsync(status, cancellationToken));
        }
        catch (ContentGenerationValidationException validationException)
        {
            return BadRequest(new { message = validationException.Message });
        }
    }

    /// <summary>
    /// The checkpoint screen's payload: the extracted structure, the material it came from, and where
    /// the run currently is. Polled while the status is <c>structuring</c> or <c>generating</c> —
    /// both are minutes long, and neither holds an HTTP connection open while it runs.
    /// </summary>
    [HttpGet("admin/content-generation/{jobId:guid}")]
    public async Task<ActionResult<ContentGenerationJobDto>> GetJob(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await contentGenerationJobService.GetJobAsync(jobId, cancellationToken);

        return job is null ? NotFound() : Ok(job);
    }

    [HttpPost("admin/content-generation")]
    public async Task<ActionResult<ContentGenerationJobDto>> Start(
        [FromBody] StartContentGenerationRequestDto requestDto,
        CancellationToken cancellationToken = default)
    {
        User.TryResolveUserId(out var actorId);

        try
        {
            var job = await contentGenerationJobService.StartAsync(requestDto, actorId, cancellationToken);

            return CreatedAtAction(nameof(GetJob), new { jobId = job.Id }, job);
        }
        catch (ContentGenerationValidationException validationException)
        {
            return BadRequest(new { message = validationException.Message });
        }
    }

    /// <summary>
    /// «Что убрать, что добавить» — the edit the whole block exists to make cheap. The structure is
    /// replaced wholesale rather than patched: the reviewer is looking at the entire document, and a
    /// per-field patch protocol would be a merge story for a draft that is meant to be disposable.
    /// </summary>
    [HttpPut("admin/content-generation/{jobId:guid}/structure")]
    public async Task<ActionResult<ContentGenerationJobDto>> UpdateStructure(
        Guid jobId,
        [FromBody] ContentStructureDto requestDto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var job = await contentGenerationJobService.UpdateStructureAsync(jobId, requestDto, cancellationToken);

            return job is null ? NotFound() : Ok(job);
        }
        catch (ContentGenerationValidationException validationException)
        {
            return BadRequest(new { message = validationException.Message });
        }
        catch (ContentGenerationStateException stateException)
        {
            return Conflict(new { message = stateException.Message });
        }
    }

    /// <summary>«Всё верно» — the only door into generation, and the only transition no worker can make.</summary>
    [HttpPost("admin/content-generation/{jobId:guid}/approve")]
    public async Task<ActionResult<ContentGenerationJobDto>> Approve(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        User.TryResolveUserId(out var actorId);

        try
        {
            var job = await contentGenerationJobService.ApproveAsync(jobId, actorId, cancellationToken);

            return job is null ? NotFound() : Ok(job);
        }
        catch (ContentGenerationValidationException validationException)
        {
            return BadRequest(new { message = validationException.Message });
        }
        catch (ContentGenerationStateException stateException)
        {
            return Conflict(new { message = stateException.Message });
        }
    }

    [HttpPost("admin/content-generation/{jobId:guid}/retry")]
    public async Task<ActionResult<ContentGenerationJobDto>> Retry(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var job = await contentGenerationJobService.RetryAsync(jobId, cancellationToken);

            return job is null ? NotFound() : Ok(job);
        }
        catch (ContentGenerationStateException stateException)
        {
            return Conflict(new { message = stateException.Message });
        }
    }
}
