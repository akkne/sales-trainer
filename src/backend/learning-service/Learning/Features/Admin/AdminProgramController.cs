using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Common.Extensions;
using Sellevate.Learning.Features.Programs.Models;
using Sellevate.Learning.Features.Programs.Services.Abstract;

namespace Sellevate.Learning.Features.Admin;

/// <summary>
/// Phase 40.17. The РОП's programme: build it, freeze it, see who is on which version
/// (docs/TENANCY/CONTENT_MODEL.md §2.5).
///
/// <para>
/// <b>Why this needs no second gate, unlike 40.15's lesson versions.</b> That controller had to
/// check the owner of each lesson, because a lesson with <c>OrganizationId IS NULL</c> is the global
/// library and an organization administrator editing it would be editing every customer's
/// curriculum. Nothing here has that shape: a programme belongs to exactly one organization by
/// construction — there is no global programme and the column is not nullable — so
/// <see cref="AuthorizationPolicies.RequireOrganizationAdministrator"/> plus the tenancy layer is
/// the whole boundary.
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
public sealed class AdminProgramController(
    IProgramVersionService programVersionService,
    IProgramEnrollmentService programEnrollmentService,
    ITenantContext tenantContext,
    ILogger<AdminProgramController> logger) : ControllerBase
{
    [HttpGet("admin/program/versions")]
    public async Task<ActionResult<IReadOnlyList<ProgramVersionSummaryDto>>> GetVersions(
        CancellationToken cancellationToken = default)
        => Ok(await programVersionService.GetVersionsAsync(cancellationToken));

    [HttpGet("admin/program/versions/{programVersionId:guid}")]
    public async Task<ActionResult<ProgramVersionDto>> GetVersion(
        Guid programVersionId, CancellationToken cancellationToken = default)
    {
        var version = await programVersionService.GetVersionAsync(programVersionId, cancellationToken);
        if (version is null) return NotFound();

        return Ok(version);
    }

    [HttpGet("admin/program/versions/{programVersionId:guid}/diff/{baselineProgramVersionId:guid}")]
    public async Task<ActionResult<ProgramDiffDto>> GetDiff(
        Guid programVersionId,
        Guid baselineProgramVersionId,
        CancellationToken cancellationToken = default)
    {
        var diff = await programVersionService.GetDiffAsync(
            baselineProgramVersionId, programVersionId, cancellationToken);
        if (diff is null) return NotFound();

        return Ok(diff);
    }

    [HttpPost("admin/program/versions/draft")]
    public async Task<ActionResult<ProgramVersionDto>> EnsureDraft(CancellationToken cancellationToken = default)
    {
        var refusal = RefuseIfNoOrganization();
        if (refusal is not null) return refusal;

        User.TryResolveUserId(out var actorId);

        var draft = await programVersionService.EnsureDraftAsync(actorId, cancellationToken);

        logger.LogInformation(
            "Program draft version opened ProgramVersionId={ProgramVersionId} VersionNumber={VersionNumber} ItemCount={ItemCount} by ActorId={ActorId}",
            draft.Id, draft.VersionNumber, draft.Items.Count, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(draft);
    }

    [HttpPost("admin/program/versions/publish")]
    public async Task<ActionResult<PublishProgramVersionResultDto>> Publish(
        CancellationToken cancellationToken = default)
    {
        var refusal = RefuseIfNoOrganization();
        if (refusal is not null) return refusal;

        User.TryResolveUserId(out var actorId);

        var result = await programVersionService.PublishAsync(actorId, cancellationToken);
        if (result is null)
        {
            return Conflict(new { message = "There is no programme draft to publish." });
        }

        logger.LogInformation(
            "Program version publish ProgramVersionId={ProgramVersionId} VersionNumber={VersionNumber} CreatedNewVersion={CreatedNewVersion} by ActorId={ActorId}",
            result.Version.Id, result.Version.VersionNumber, result.CreatedNewVersion,
            User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(result);
    }

    [HttpGet("admin/program/enrollments")]
    public async Task<ActionResult<IReadOnlyList<ProgramEnrollmentDto>>> GetEnrollments(
        CancellationToken cancellationToken = default)
        => Ok(await programEnrollmentService.GetEnrollmentsAsync(cancellationToken));

    /// <summary>
    /// Puts one learner on the newest published programme version. Idempotent, and it never moves
    /// somebody who already has a pin — that is a call only the learner can make, on themselves
    /// (<c>POST /program/switch</c>). The response says which version they are actually on, so a
    /// caller can tell "enrolled them" from "they were already mid-course on version 3".
    /// </summary>
    [HttpPost("admin/program/enrollments")]
    public async Task<ActionResult<ProgramEnrollmentDto>> Enroll(
        [FromBody] EnrollUserRequestDto requestDto,
        CancellationToken cancellationToken = default)
    {
        var refusal = RefuseIfNoOrganization();
        if (refusal is not null) return refusal;

        if (requestDto.UserId == Guid.Empty)
        {
            return BadRequest(new { message = "userId is required." });
        }

        var enrollment = await programEnrollmentService.EnrollAsync(requestDto.UserId, cancellationToken);
        if (enrollment is null)
        {
            return Conflict(new { message = "This organization has no published programme version yet." });
        }

        return Ok(enrollment);
    }

    /// <summary>
    /// Platform staff satisfy <c>RequireOrgAdmin</c> without holding any organization, which is
    /// deliberate (AuthorizationPolicies) and harmless for reads — the tenancy layer hands them
    /// everything or nothing. A write is different: a programme belongs to one organization, and
    /// with none in context the save guard would throw and the caller would see a 500 describing an
    /// internal invariant. Refusing here says the true thing instead: pick an organization first
    /// (impersonation, 40.9).
    /// </summary>
    private ActionResult? RefuseIfNoOrganization()
        => tenantContext.OrganizationId is null
            ? Forbid()
            : null;
}
