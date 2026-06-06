using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesTrainer.Api.Features.Voice.Models;
using SalesTrainer.Api.Features.Voice.Services.Abstract;

namespace SalesTrainer.Api.Features.Admin;

[ApiController]
[Route("admin/voice")]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminVoiceUsageController(IVoiceUsageService voiceUsageService) : ControllerBase
{
    [HttpGet("usage")]
    public async Task<ActionResult<AdminVoiceUsageDto>> GetUsage(CancellationToken cancellationToken)
    {
        var usage = await voiceUsageService.GetAllUsersUsageAsync(cancellationToken);
        return Ok(usage);
    }
}
