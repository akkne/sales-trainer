using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Gamification.Common.Constants;
using Sellevate.Gamification.Common.Extensions;
using Sellevate.Gamification.Features.Achievements.Models;
using Sellevate.Gamification.Features.Achievements.Services.Abstract;

namespace Sellevate.Gamification.Features.Achievements;

/// <summary>
/// Phase 40.13: <see cref="TenantScopedAttribute"/>. Every route here reads or writes rows that
/// belong to one organization, so a request without the gateway-validated organization header is
/// refused by the tenant middleware with a 403 instead of reaching a query filter that would match
/// nothing and answer with a plausible-looking empty result.
/// </summary>
[ApiController]
[Route(RouteConstants.ProfileAchievements)]
[Authorize]
[TenantScoped]
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
