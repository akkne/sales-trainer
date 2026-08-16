using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Gamification.Common.Constants;
using Sellevate.Gamification.Common.Extensions;
using Sellevate.Gamification.Features.League.Models;
using Sellevate.Gamification.Features.League.Services.Abstract;

namespace Sellevate.Gamification.Features.League;

/// <summary>
/// Phase 40.13: <see cref="TenantScopedAttribute"/>. Every route here reads or writes rows that
/// belong to one organization, so a request without the gateway-validated organization header is
/// refused by the tenant middleware with a 403 instead of reaching a query filter that would match
/// nothing and answer with a plausible-looking empty result.
/// </summary>
[ApiController]
[Route(RouteConstants.League)]
[Authorize]
[TenantScoped]
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
