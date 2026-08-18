using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.Ai.Features.Voice.Models;
using Sellevate.Ai.Features.Voice.Services.Abstract;
using Sellevate.Ai.Common.Constants;

namespace Sellevate.Ai.Features.Voice;

/// <summary>
/// Phase 40.6 audit: the voice usage and cost view across every user, Sellevate-staff-only.
/// Scoped to the caller's organization by the repository since 40.11 — a platform superadmin reaches
/// another customer's numbers by impersonating into it.
/// </summary>
[ApiController]
[Route("admin/voice")]
[Authorize(Policy = AuthorizationPolicies.RequirePlatformAdministrator)]
public sealed class AdminVoiceUsageController(IVoiceUsageService voiceUsageService) : ControllerBase
{
    [HttpGet("usage")]
    public async Task<ActionResult<AdminVoiceUsageDto>> GetUsage(CancellationToken cancellationToken)
    {
        var usage = await voiceUsageService.GetAllUsersUsageAsync(cancellationToken);
        return Ok(usage);
    }
}
