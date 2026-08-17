using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.Learning.Common.Extensions;
using Sellevate.Learning.Features.Programs.Models;
using Sellevate.Learning.Features.Programs.Services.Abstract;

namespace Sellevate.Learning.Features.Programs;

/// <summary>
/// Phase 40.17. A learner's own programme, and the only way a pin ever moves
/// (docs/TENANCY/CONTENT_MODEL.md §2.5).
///
/// <para>
/// Both routes act on the caller identified by the token and take no user id, which is what makes
/// "an administrator cannot rearrange somebody's programme mid-course" a property of the surface
/// rather than of the current UI.
/// </para>
/// </summary>
[ApiController]
[Authorize]
public sealed class ProgramController(
    IProgramEnrollmentService programEnrollmentService,
    ILogger<ProgramController> logger) : ControllerBase
{
    [HttpGet("program")]
    public async Task<ActionResult<MyProgramDto>> GetMyProgram(CancellationToken cancellationToken = default)
    {
        if (!User.TryResolveUserId(out var userId)) return Unauthorized();

        return Ok(await programEnrollmentService.GetMyProgramAsync(userId, cancellationToken));
    }

    /// <summary>
    /// Moves the caller's own pin to the version they named. A 409 means the target is not a
    /// published version of their organization, is the one they are already on, or they have no pin
    /// at all — three ways of saying "there is nothing here to switch to", none of which should
    /// resolve into moving them somewhere they did not ask for.
    /// </summary>
    [HttpPost("program/switch")]
    public async Task<ActionResult<MyProgramDto>> Switch(
        [FromBody] SwitchProgramVersionRequestDto requestDto,
        CancellationToken cancellationToken = default)
    {
        if (!User.TryResolveUserId(out var userId)) return Unauthorized();

        if (requestDto.TargetProgramVersionId == Guid.Empty)
        {
            return BadRequest(new { message = "targetProgramVersionId is required." });
        }

        var program = await programEnrollmentService.SwitchAsync(
            userId, requestDto.TargetProgramVersionId, cancellationToken);

        if (program is null)
        {
            return Conflict(new { message = "That programme version cannot be switched to." });
        }

        logger.LogInformation(
            "Program switch accepted by learner UserId={UserId} ProgramVersionId={ProgramVersionId}",
            userId, requestDto.TargetProgramVersionId);

        return Ok(program);
    }
}
