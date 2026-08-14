using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.Ai.Features.Voice.Models;
using Sellevate.Ai.Features.Voice.Services.Abstract;

namespace Sellevate.Ai.Features.Voice;

// Phase 40.6 audit: platform-wide voice usage/cost view across all users — Sellevate-staff-only.
[ApiController]
[Route("admin/voice")]
[Authorize(Policy = "RequireSuperAdmin")]
public sealed class AdminVoiceUsageController(IVoiceUsageService voiceUsageService) : ControllerBase
{
    [HttpGet("usage")]
    public async Task<ActionResult<AdminVoiceUsageDto>> GetUsage(CancellationToken cancellationToken)
    {
        var usage = await voiceUsageService.GetAllUsersUsageAsync(cancellationToken);
        return Ok(usage);
    }
}
