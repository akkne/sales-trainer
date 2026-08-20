using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Common.Extensions;
using Sellevate.Learning.Features.DialogReviews.Models;
using Sellevate.Learning.Features.DialogReviews.Services.Abstract;

namespace Sellevate.Learning.Features.DialogReviews;

/// <summary>
/// Phase 40.25. The manager's half of the loop: read what the РОП said, and dispute a grade
/// (docs/TENANCY/ASSIGNMENTS.md §4.1).
///
/// <para>
/// <b>The route takes no user id and never will</b>, the same shape <c>AssignmentsController</c>
/// uses: the caller is whoever the token says. That is also the entire authorization of the dispute
/// path — a person may only dispute a grade on a conversation the score row says was theirs, and the
/// service compares against the resolved id rather than anything in the body.
/// </para>
/// </summary>
[ApiController]
[TenantScoped]
[Authorize]
public sealed class DialogReviewsController(IDialogReviewService dialogReviewService) : ControllerBase
{
    /// <summary>
    /// Coaching notes addressed to the caller and disputes they filed, newest first. One list rather
    /// than two, because both are "the conversation about my conversations" and a manager who has to
    /// look in two places will look in neither.
    /// </summary>
    [HttpGet("dialog-reviews")]
    public async Task<ActionResult<IReadOnlyList<DialogReviewNoteDto>>> GetMine(
        CancellationToken cancellationToken = default)
    {
        if (!User.TryResolveUserId(out var userId)) return Unauthorized();

        return Ok(await dialogReviewService.GetForUserAsync(userId, cancellationToken));
    }

    /// <summary>
    /// «Менеджер оспаривает оценку ИИ» — the mechanism the roadmap says cannot be skipped, because
    /// the first genuinely disputed score otherwise costs the product the team's trust in every
    /// number it shows.
    /// </summary>
    [HttpPost("dialog-reviews/disputes")]
    public async Task<ActionResult<DialogReviewNoteDto>> Dispute(
        [FromBody] CreateScoreDisputeRequestDto requestDto,
        CancellationToken cancellationToken = default)
    {
        if (!User.TryResolveUserId(out var userId)) return Unauthorized();

        try
        {
            return Ok(await dialogReviewService.CreateScoreDisputeAsync(userId, requestDto, cancellationToken));
        }
        catch (DialogReviewValidationException validationException)
        {
            return BadRequest(new { message = validationException.Message });
        }
    }

    [HttpPost("dialog-reviews/{noteId:guid}/acknowledge")]
    public async Task<ActionResult<DialogReviewNoteDto>> Acknowledge(
        Guid noteId,
        CancellationToken cancellationToken = default)
    {
        if (!User.TryResolveUserId(out var userId)) return Unauthorized();

        var note = await dialogReviewService.AcknowledgeCoachingNoteAsync(userId, noteId, cancellationToken);

        return note is null ? NotFound() : Ok(note);
    }
}
