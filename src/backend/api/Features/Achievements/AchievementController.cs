using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesTrainer.Api.Features.Achievements.Models;
using SalesTrainer.Api.Features.Achievements.Services.Abstract;

namespace SalesTrainer.Api.Features.Achievements;

[ApiController]
[Route("profile/achievements")]
[Authorize]
public sealed class AchievementController(IAchievementService achievementService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AchievementDto>>> GetAchievements(
        CancellationToken cancellationToken = default)
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(rawUserId, out var userId))
            return Unauthorized();

        var achievements = await achievementService.GetAchievementsForUserAsync(userId, cancellationToken);
        return Ok(achievements);
    }
}
