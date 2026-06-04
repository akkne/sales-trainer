using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesTrainer.Api.Features.Voice.Models;
using SalesTrainer.Api.Features.Voice.Services.Abstract;

namespace SalesTrainer.Api.Features.Admin;

[ApiController]
[Route("admin/voice")]
[Authorize(Policy = "RequireAdmin")]
public class AdminVoiceUsageController(IVoiceUsageService voiceUsageService) : ControllerBase
{
    /// <summary>Per-user voice minute spend (daily / monthly / total), sorted by monthly usage.</summary>
    [HttpGet("usage")]
    public async Task<ActionResult<AdminVoiceUsageDto>> GetUsage(CancellationToken ct)
    {
        var usage = await voiceUsageService.GetAllUsersUsageAsync(ct);
        return Ok(usage);
    }
}
