using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SalesTrainer.Api.Features.Achievements;

[ApiController]
[Route("profile/achievements")]
[Authorize]
public class AchievementController(AchievementService achievementService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AchievementDto>>> GetAchievements()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(rawUserId, out var userId))
            return Unauthorized();

        var achievements = await achievementService.GetAchievementsForUserAsync(userId);
        return Ok(achievements);
    }
}
