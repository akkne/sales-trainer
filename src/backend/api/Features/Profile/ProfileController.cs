using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SalesTrainer.Api.Features.Profile;

[ApiController]
[Route("profile")]
[Authorize]
public class ProfileController(ProfileService profileService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<UserProfileStatsDto>> GetProfileStats()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(rawUserId, out var userId))
            return Unauthorized();

        try
        {
            var profileStats = await profileService.GetProfileStatsForUserAsync(userId);
            return Ok(profileStats);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
    }
}
