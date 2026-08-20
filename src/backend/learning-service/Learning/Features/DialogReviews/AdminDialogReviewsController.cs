using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Common.Extensions;
using Sellevate.Learning.Features.DialogReviews.Models;
using Sellevate.Learning.Features.DialogReviews.Services.Abstract;

namespace Sellevate.Learning.Features.DialogReviews;

/// <summary>
/// Phase 40.25. The РОП's half of the loop: comment on a fragment and send it, and rule on the
/// disputes that come back (docs/TENANCY/ASSIGNMENTS.md §4.1).
///
/// <para>
/// One gate — <see cref="AuthorizationPolicies.RequireOrganizationAdministrator"/> — as for
/// assignments, and for the same reason: every row is strict tenant data with no global
/// counterpart. The organization comes from <see cref="ITenantContext"/> and never from the request
/// (docs/TENANCY/TENANCY.md §1.3); the conversation's owner comes from the score row rather than
/// from the body, so a note cannot be addressed at somebody else's employee.
/// </para>
/// </summary>
[ApiController]
[TenantScoped]
[Authorize(Policy = AuthorizationPolicies.RequireOrganizationAdministrator)]
public sealed class AdminDialogReviewsController(
    IDialogReviewService dialogReviewService,
    ITenantContext tenantContext) : ControllerBase
{
    /// <summary>
    /// The queue. <c>?kind=score_dispute&amp;status=open</c> is the one the screen opens on: the
    /// disputes waiting for a verdict.
    /// </summary>
    [HttpGet("admin/dialog-reviews")]
    public async Task<ActionResult<IReadOnlyList<DialogReviewNoteDto>>> GetAll(
        [FromQuery] string? kind = null,
        [FromQuery] string? status = null,
        [FromQuery] string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await dialogReviewService.GetForOrganizationAsync(kind, status, sessionId, cancellationToken));
        }
        catch (DialogReviewValidationException validationException)
        {
            return BadRequest(new { message = validationException.Message });
        }
    }

    /// <summary>
    /// «РОП выделяет фрагмент диалога, комментирует, отправляет менеджеру» — three lines out of a
    /// transcript, with the lines themselves copied into the row so the note still reads next month.
    /// </summary>
    [HttpPost("admin/dialog-reviews")]
    public async Task<ActionResult<DialogReviewNoteDto>> CreateCoachingNote(
        [FromBody] CreateCoachingNoteRequestDto requestDto,
        CancellationToken cancellationToken = default)
    {
        var refusal = RefuseIfNoOrganization();
        if (refusal is not null) return refusal;

        User.TryResolveUserId(out var actorId);

        try
        {
            return Ok(await dialogReviewService.CreateCoachingNoteAsync(actorId, requestDto, cancellationToken));
        }
        catch (DialogReviewValidationException validationException)
        {
            return BadRequest(new { message = validationException.Message });
        }
    }

    [HttpPost("admin/dialog-reviews/{noteId:guid}/resolve")]
    public async Task<ActionResult<DialogReviewNoteDto>> ResolveDispute(
        Guid noteId,
        [FromBody] ResolveScoreDisputeRequestDto requestDto,
        CancellationToken cancellationToken = default)
    {
        var refusal = RefuseIfNoOrganization();
        if (refusal is not null) return refusal;

        User.TryResolveUserId(out var actorId);

        try
        {
            var note = await dialogReviewService.ResolveScoreDisputeAsync(
                actorId, noteId, requestDto, cancellationToken);

            return note is null ? NotFound() : Ok(note);
        }
        catch (DialogReviewValidationException validationException)
        {
            return BadRequest(new { message = validationException.Message });
        }
    }

    /// <summary>
    /// Platform staff satisfy <c>RequireOrgAdmin</c> without holding any organization, which is
    /// deliberate and harmless for reads. A write is different: the row belongs to one organization,
    /// and with none in context the save guard would throw and the caller would see a 500 describing
    /// an internal invariant. Same shape as <c>AdminAssignmentsController</c>.
    /// </summary>
    private ActionResult? RefuseIfNoOrganization()
        => tenantContext.OrganizationId is null
            ? Forbid()
            : null;
}
