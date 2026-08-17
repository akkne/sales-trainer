using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Common.Extensions;
using Sellevate.Learning.Features.Lessons.Models;
using Sellevate.Learning.Features.Lessons.Services.Abstract;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.Admin;

/// <summary>
/// Phase 40.15. Authoring endpoints for immutable lesson versions.
///
/// <para>
/// <b>Who may publish, and why the gate is two-part.</b> The controller carries
/// <see cref="AuthorizationPolicies.RequireOrganizationAdministrator"/>, which admits an
/// organization's own administrator as well as any Sellevate platform administrator. That policy
/// alone is not enough, because the two kinds of lesson in this table are not equally owned: a row
/// with <c>OrganizationId IS NULL</c> is the global library every customer reads, and an
/// organization administrator publishing into it would be editing the curriculum of every other
/// customer. So each write additionally checks the lesson's owner and requires platform
/// administrator rights for global content. The reverse direction needs no check: an organization's
/// own lessons are already invisible to other organizations through the query filter and the RLS
/// policy, so "which organization's lesson is this" was decided before the request reached here.
/// </para>
///
/// <para>
/// The organization is never read from the body, the query string or the route — it comes from
/// <c>ITenantContext</c>, filled by the gateway-validated header (docs/TENANCY/TENANCY.md §1.3).
/// </para>
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.RequireOrganizationAdministrator)]
public sealed class AdminLessonVersionsController(
    LearningDbContext database,
    ILessonVersionService lessonVersionService,
    ILogger<AdminLessonVersionsController> logger) : ControllerBase
{
    [HttpGet("admin/lessons/{lessonId:guid}/versions")]
    public async Task<ActionResult<IReadOnlyList<LessonVersionSummaryDto>>> GetVersions(
        Guid lessonId, CancellationToken cancellationToken = default)
    {
        var versions = await lessonVersionService.GetVersionsAsync(lessonId, cancellationToken);
        if (versions is null) return NotFound(new { message = $"Lesson '{lessonId}' not found." });

        return Ok(versions);
    }

    [HttpGet("admin/lessons/{lessonId:guid}/versions/{versionId:guid}")]
    public async Task<ActionResult<LessonVersionDto>> GetVersion(
        Guid lessonId, Guid versionId, CancellationToken cancellationToken = default)
    {
        var version = await lessonVersionService.GetVersionAsync(lessonId, versionId, cancellationToken);
        if (version is null) return NotFound();

        return Ok(version);
    }

    [HttpPost("admin/lessons/{lessonId:guid}/versions/draft")]
    public async Task<ActionResult<LessonVersionDto>> EnsureDraft(
        Guid lessonId, CancellationToken cancellationToken = default)
    {
        var refusal = await RefuseIfNotAllowedToAuthorAsync(lessonId, cancellationToken);
        if (refusal is not null) return refusal;

        User.TryResolveUserId(out var actorId);

        var draft = await lessonVersionService.EnsureDraftAsync(lessonId, actorId, cancellationToken);
        if (draft is null) return NotFound(new { message = $"Lesson '{lessonId}' not found." });

        logger.LogInformation(
            "Lesson draft version opened LessonId={LessonId} VersionId={VersionId} VersionNumber={VersionNumber} by ActorId={ActorId}",
            lessonId, draft.Id, draft.VersionNumber, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(draft);
    }

    [HttpPost("admin/lessons/{lessonId:guid}/versions/publish")]
    public async Task<ActionResult<PublishLessonVersionResultDto>> Publish(
        Guid lessonId,
        [FromBody] PublishLessonVersionRequestDto? requestDto = null,
        CancellationToken cancellationToken = default)
    {
        var refusal = await RefuseIfNotAllowedToAuthorAsync(lessonId, cancellationToken);
        if (refusal is not null) return refusal;

        User.TryResolveUserId(out var actorId);

        var result = await lessonVersionService.PublishAsync(
            lessonId, requestDto?.IsBreaking ?? false, actorId, cancellationToken);
        if (result is null) return NotFound(new { message = $"Lesson '{lessonId}' not found." });

        logger.LogInformation(
            "Lesson version publish LessonId={LessonId} VersionNumber={VersionNumber} CreatedNewVersion={CreatedNewVersion} IsBreaking={IsBreaking} ContentHash={ContentHash} by ActorId={ActorId}",
            lessonId, result.Version.VersionNumber, result.CreatedNewVersion, result.Version.IsBreaking,
            result.Version.ContentHash, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(result);
    }

    /// <summary>
    /// Returns a refusal when the caller may not author this lesson, and <see langword="null"/>
    /// when they may. A missing lesson is left to the service so that "not found" and "not yours"
    /// stay the same answer to an outsider.
    /// </summary>
    private async Task<ActionResult?> RefuseIfNotAllowedToAuthorAsync(Guid lessonId, CancellationToken cancellationToken)
    {
        var owningOrganizationIds = await database.Lessons
            .Where(lesson => lesson.Id == lessonId)
            .Select(lesson => lesson.OrganizationId)
            .ToListAsync(cancellationToken);

        if (owningOrganizationIds.Count == 0) return null;

        var isGlobalLesson = owningOrganizationIds[0] is null;
        if (isGlobalLesson && !IsPlatformAdministrator())
        {
            return Forbid();
        }

        return null;
    }

    private bool IsPlatformAdministrator()
        => User.IsInRole(AuthorizationPolicies.AdministratorRole)
           || User.IsInRole(AuthorizationPolicies.SuperAdministratorRole);
}
