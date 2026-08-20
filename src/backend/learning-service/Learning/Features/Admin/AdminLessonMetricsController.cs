using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Lessons.Models;
using Sellevate.Learning.Features.Lessons.Services.Abstract;

namespace Sellevate.Learning.Features.Admin;

/// <summary>
/// Phase 40.16. The read side of version-bound progress: accuracy per lesson, segmented by the
/// publishes that were flagged breaking (docs/TENANCY/CONTENT_MODEL.md §2.3-§2.4).
///
/// <para>
/// Unlike <see cref="AdminLessonVersionsController"/> this endpoint needs no second, platform-level
/// gate for global lessons. That controller writes content every customer reads; this one only
/// counts the caller's own organization's attempts, and the row-level-security policy on
/// <c>UserExerciseAttempts</c> is plain equality — so an organization administrator asking about a
/// global lesson gets their own team's numbers and nobody else's, which is exactly the question a
/// РОП is entitled to ask.
/// </para>
///
/// <para>
/// The organization is never read from the body, the query string or the route — it comes from
/// <c>ITenantContext</c>, filled by the gateway-validated header (docs/TENANCY/TENANCY.md §1.3).
/// </para>
/// </summary>
[ApiController]
[TenantScoped]
[Authorize(Policy = AuthorizationPolicies.RequireOrganizationAdministrator)]
public sealed class AdminLessonMetricsController(ILessonAccuracyService lessonAccuracyService) : ControllerBase
{
    [HttpGet("admin/lessons/{lessonId:guid}/accuracy")]
    public async Task<ActionResult<LessonAccuracySeriesDto>> GetAccuracySeries(
        Guid lessonId, CancellationToken cancellationToken = default)
    {
        var series = await lessonAccuracyService.GetAccuracySeriesAsync(lessonId, cancellationToken);
        if (series is null) return NotFound(new { message = $"Lesson '{lessonId}' not found." });

        return Ok(series);
    }
}
