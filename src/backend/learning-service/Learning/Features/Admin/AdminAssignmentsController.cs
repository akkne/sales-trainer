using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Common.Extensions;
using Sellevate.Learning.Features.Assignments.Models;
using Sellevate.Learning.Features.Assignments.Services.Abstract;

namespace Sellevate.Learning.Features.Admin;

/// <summary>
/// Phase 40.21. The РОП's assignments: write one, say who it is for and what counts as done, issue
/// it, close it (docs/TENANCY/ASSIGNMENTS.md §1).
///
/// <para>
/// <b>One gate, as for programmes and unlike lesson versions.</b> The second gate on lesson
/// publishing exists because a lesson with <c>OrganizationId IS NULL</c> is the global library and an
/// organization administrator editing it would be editing every customer's curriculum. An assignment
/// has no such shape: it belongs to exactly one organization by construction, the column is not
/// nullable, and there is no global assignment — so
/// <see cref="AuthorizationPolicies.RequireOrganizationAdministrator"/> plus the tenancy layer is the
/// whole boundary.
/// </para>
///
/// <para>
/// The organization is never read from the body, the query string or the route; it comes from
/// <see cref="ITenantContext"/>, filled from the gateway-validated header
/// (docs/TENANCY/TENANCY.md §1.3).
/// </para>
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.RequireOrganizationAdministrator)]
public sealed class AdminAssignmentsController(
    IAssignmentService assignmentService,
    IAssignmentDashboardService assignmentDashboardService,
    ITenantContext tenantContext,
    ILogger<AdminAssignmentsController> logger) : ControllerBase
{
    [HttpGet("admin/assignments")]
    public async Task<ActionResult<IReadOnlyList<AssignmentSummaryDto>>> GetAssignments(
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await assignmentService.GetAssignmentsAsync(status, cancellationToken));
        }
        catch (AssignmentValidationException validationException)
        {
            return BadRequest(new { message = validationException.Message });
        }
    }

    [HttpGet("admin/assignments/{assignmentId:guid}")]
    public async Task<ActionResult<AssignmentDto>> GetAssignment(
        Guid assignmentId,
        CancellationToken cancellationToken = default)
    {
        var assignment = await assignmentService.GetAssignmentAsync(assignmentId, cancellationToken);

        return assignment is null ? NotFound() : Ok(assignment);
    }

    [HttpGet("admin/assignments/{assignmentId:guid}/progress")]
    public async Task<ActionResult<IReadOnlyList<AssignmentProgressDto>>> GetProgress(
        Guid assignmentId,
        CancellationToken cancellationToken = default)
    {
        var progressRecords = await assignmentService.GetProgressAsync(assignmentId, cancellationToken);

        return progressRecords is null ? NotFound() : Ok(progressRecords);
    }

    /// <summary>
    /// Phase 40.25. The screen the РОП actually opens: the funnel, the named people behind it, and
    /// every wave of the repeat series next to each other (docs/TENANCY/ASSIGNMENTS.md §4).
    ///
    /// <para>
    /// It supersedes nothing — <c>/progress</c> stays, because it is the raw, name-free list and the
    /// only one of the two that cannot be affected by identity-service being down.
    /// </para>
    /// </summary>
    [HttpGet("admin/assignments/{assignmentId:guid}/dashboard")]
    public async Task<ActionResult<AssignmentDashboardDto>> GetDashboard(
        Guid assignmentId,
        CancellationToken cancellationToken = default)
    {
        var dashboard = await assignmentDashboardService.GetDashboardAsync(assignmentId, cancellationToken);

        return dashboard is null ? NotFound() : Ok(dashboard);
    }

    [HttpPost("admin/assignments")]
    public async Task<ActionResult<AssignmentDto>> Create(
        [FromBody] CreateAssignmentRequestDto requestDto,
        CancellationToken cancellationToken = default)
    {
        var refusal = RefuseIfNoOrganization();
        if (refusal is not null) return refusal;

        User.TryResolveUserId(out var actorId);

        try
        {
            var assignment = await assignmentService.CreateAsync(actorId, requestDto, cancellationToken);

            logger.LogInformation(
                "Assignment created AssignmentId={AssignmentId} ActorId={ActorId}", assignment.Id, actorId);

            return Ok(assignment);
        }
        catch (AssignmentValidationException validationException)
        {
            return BadRequest(new { message = validationException.Message });
        }
    }

    [HttpPut("admin/assignments/{assignmentId:guid}")]
    public async Task<ActionResult<AssignmentDto>> Update(
        Guid assignmentId,
        [FromBody] UpdateAssignmentRequestDto requestDto,
        CancellationToken cancellationToken = default)
    {
        var refusal = RefuseIfNoOrganization();
        if (refusal is not null) return refusal;

        try
        {
            return Respond(await assignmentService.UpdateAsync(assignmentId, requestDto, cancellationToken));
        }
        catch (AssignmentValidationException validationException)
        {
            return BadRequest(new { message = validationException.Message });
        }
        catch (AssignmentAudienceUnavailableException unavailableException)
        {
            // Editing an issued assignment re-resolves its audience (40.23), so this route inherits
            // the same failure as issuing. Nothing was written: the top-up happens inside the same
            // transaction as the edit.
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = unavailableException.Message });
        }
    }

    /// <summary>
    /// Phase 40.23. Issuing is the moment the audience rule becomes named people, so this route can
    /// now fail in a way none of the others can: identity-service may not answer, and then nobody
    /// knows who works here. That is a 503 rather than a 500 — nothing is wrong with the request and
    /// nothing is wrong with the assignment, and the honest instruction is "press it again".
    /// </summary>
    [HttpPost("admin/assignments/{assignmentId:guid}/activate")]
    public async Task<ActionResult<AssignmentDto>> Activate(
        Guid assignmentId,
        CancellationToken cancellationToken = default)
    {
        var refusal = RefuseIfNoOrganization();
        if (refusal is not null) return refusal;

        try
        {
            return Respond(await assignmentService.ActivateAsync(assignmentId, cancellationToken));
        }
        catch (AssignmentValidationException validationException)
        {
            return BadRequest(new { message = validationException.Message });
        }
        catch (AssignmentAudienceUnavailableException unavailableException)
        {
            logger.LogWarning(
                unavailableException,
                "Assignment {AssignmentId} was not issued: the organization roster could not be read.",
                assignmentId);

            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = unavailableException.Message });
        }
    }

    /// <summary>
    /// Phase 40.23. The one-click nudge docs/TENANCY/ASSIGNMENTS.md §5 asks for, addressed at
    /// everybody on the assignment who has not finished.
    /// </summary>
    [HttpPost("admin/assignments/{assignmentId:guid}/remind")]
    public async Task<ActionResult<AssignmentReminderResultDto>> Remind(
        Guid assignmentId,
        CancellationToken cancellationToken = default)
    {
        var refusal = RefuseIfNoOrganization();
        if (refusal is not null) return refusal;

        try
        {
            var result = await assignmentService.RemindAsync(assignmentId, cancellationToken);

            return result is null ? NotFound() : Ok(result);
        }
        catch (AssignmentValidationException validationException)
        {
            return Conflict(new { message = validationException.Message });
        }
    }

    [HttpPost("admin/assignments/{assignmentId:guid}/close")]
    public async Task<ActionResult<AssignmentDto>> Close(
        Guid assignmentId,
        CancellationToken cancellationToken = default)
    {
        var refusal = RefuseIfNoOrganization();
        if (refusal is not null) return refusal;

        return Respond(await assignmentService.CloseAsync(assignmentId, cancellationToken));
    }

    [HttpDelete("admin/assignments/{assignmentId:guid}")]
    public async Task<ActionResult> Delete(
        Guid assignmentId,
        CancellationToken cancellationToken = default)
    {
        var refusal = RefuseIfNoOrganization();
        if (refusal is not null) return refusal;

        var result = await assignmentService.DeleteAsync(assignmentId, cancellationToken);

        return result.Outcome switch
        {
            AssignmentWriteOutcome.NotFound => NotFound(),
            AssignmentWriteOutcome.RejectedByStatus => Conflict(new { message = result.RefusalReason }),
            _ => NoContent(),
        };
    }

    private ActionResult<AssignmentDto> Respond(AssignmentWriteResult result)
        => result.Outcome switch
        {
            AssignmentWriteOutcome.NotFound => NotFound(),
            AssignmentWriteOutcome.RejectedByStatus => Conflict(new { message = result.RefusalReason }),
            _ => Ok(result.Assignment),
        };

    /// <summary>
    /// Platform staff satisfy <c>RequireOrgAdmin</c> without holding any organization, which is
    /// deliberate (<see cref="AuthorizationPolicies"/>) and harmless for reads — the tenancy layer
    /// hands them everything or nothing. A write is different: an assignment belongs to one
    /// organization, and with none in context the save guard would throw and the caller would see a
    /// 500 describing an internal invariant. Refusing here says the true thing instead: pick an
    /// organization first (impersonation, 40.9). Same shape as <c>AdminProgramController</c>.
    /// </summary>
    private ActionResult? RefuseIfNoOrganization()
        => tenantContext.OrganizationId is null
            ? Forbid()
            : null;
}
