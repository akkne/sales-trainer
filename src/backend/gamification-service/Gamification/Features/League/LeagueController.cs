using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.Gamification.Common.Constants;
using Sellevate.Gamification.Common.Extensions;
using Sellevate.Gamification.Features.League.Models;
using Sellevate.Gamification.Features.League.Services.Abstract;

namespace Sellevate.Gamification.Features.League;

[ApiController]
[Route(RouteConstants.League)]
[Authorize]
public sealed class LeagueController(ILeagueService leagueService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CurrentLeagueResponseDto>> GetCurrentLeague(CancellationToken cancellationToken = default)
    {
        if (!User.TryResolveUserId(out var userId))
        {
            return Unauthorized();
        }

        var currentLeague = await leagueService.GetCurrentLeagueForUserAsync(userId, cancellationToken);
        return Ok(currentLeague);
    }
}
