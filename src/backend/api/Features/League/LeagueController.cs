using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesTrainer.Api.Features.League.Models;
using SalesTrainer.Api.Features.League.Services.Abstract;

namespace SalesTrainer.Api.Features.League;

[ApiController]
[Route("league")]
[Authorize]
public class LeagueController(ILeagueService leagueService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CurrentLeagueResponseDto>> GetCurrentLeague()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(rawUserId, out var userId))
            return Unauthorized();

        var currentLeague = await leagueService.GetCurrentLeagueForUserAsync(userId);
        return Ok(currentLeague);
    }
}
