using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.Gamification.Common.Constants;
using Sellevate.Gamification.Common.Extensions;
using Sellevate.Gamification.Features.Gamification.Models;
using Sellevate.Gamification.Features.Gamification.Services.Abstract;

namespace Sellevate.Gamification.Features.Gamification;

[ApiController]
[Route(RouteConstants.GamificationProgress)]
[Authorize]
public sealed class GamificationController(IGamificationProgressService progressService) : ControllerBase
{
    [HttpGet("progress")]
    public async Task<ActionResult<GamificationProgressDto>> GetProgress(CancellationToken cancellationToken = default)
    {
        if (!User.TryResolveUserId(out var userId))
        {
            return Unauthorized();
        }

        var progress = await progressService.GetProgressForUserAsync(userId, cancellationToken);
        return Ok(progress);
    }
}
