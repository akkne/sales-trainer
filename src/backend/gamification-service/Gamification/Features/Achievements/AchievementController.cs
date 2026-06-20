using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.Gamification.Common.Constants;
using Sellevate.Gamification.Common.Extensions;
using Sellevate.Gamification.Features.Achievements.Models;
using Sellevate.Gamification.Features.Achievements.Services.Abstract;

namespace Sellevate.Gamification.Features.Achievements;

[ApiController]
[Route(RouteConstants.ProfileAchievements)]
[Authorize]
public sealed class AchievementController(IAchievementService achievementService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AchievementDto>>> GetAchievements(CancellationToken cancellationToken = default)
    {
        if (!User.TryResolveUserId(out var userId))
        {
            return Unauthorized();
        }

        var achievements = await achievementService.GetAchievementsForUserAsync(userId, cancellationToken);
        return Ok(achievements);
    }
}
