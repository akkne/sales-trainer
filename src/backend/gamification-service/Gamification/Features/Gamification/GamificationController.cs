using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Gamification.Common.Constants;
using Sellevate.Gamification.Common.Extensions;
using Sellevate.Gamification.Features.Gamification.Models;
using Sellevate.Gamification.Features.Gamification.Services.Abstract;

namespace Sellevate.Gamification.Features.Gamification;

/// <summary>
/// Phase 40.13: <see cref="TenantScopedAttribute"/>. Every route here reads or writes rows that
/// belong to one organization, so a request without the gateway-validated organization header is
/// refused by the tenant middleware with a 403 instead of reaching a query filter that would match
/// nothing and answer with a plausible-looking empty result.
/// </summary>
[ApiController]
[Route(RouteConstants.GamificationProgress)]
[Authorize]
[TenantScoped]
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
