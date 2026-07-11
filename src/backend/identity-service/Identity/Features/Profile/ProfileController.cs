using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.Identity.Features.Profile.Models;
using Sellevate.Identity.Features.Profile.Services.Abstract;

namespace Sellevate.Identity.Features.Profile;

[ApiController]
[Route("profile")]
[Authorize]
public class ProfileController(IProfileService profileService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<UserProfileStatsDto>> GetProfileStats()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(rawUserId, out var userId))
        {
            return Unauthorized();
        }

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

    [HttpPut("persona")]
    public async Task<IActionResult> UpdatePersona([FromBody] UpdatePersonaRequestDto request)
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(rawUserId, out var userId))
        {
            return Unauthorized();
        }

        if (!ValidPersonas.Contains(request.Persona))
        {
            return BadRequest(new { message = "Invalid persona value." });
        }

        await profileService.UpdatePersonaForUserAsync(userId, request.Persona);
        return NoContent();
    }

    [HttpPut]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequestDto request)
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(rawUserId, out var userId))
        {
            return Unauthorized();
        }

        var displayName = request.DisplayName?.Trim() ?? "";

        if (displayName.Length == 0)
        {
            return BadRequest(new { message = "Display name is required." });
        }

        if (displayName.Length > 100)
        {
            return BadRequest(new { message = "Display name must be 100 characters or fewer." });
        }

        var persona = string.IsNullOrWhiteSpace(request.Persona) ? null : request.Persona;

        if (persona is not null && !ValidPersonas.Contains(persona))
        {
            return BadRequest(new { message = "Invalid persona value." });
        }

        try
        {
            await profileService.UpdateProfileForUserAsync(userId, displayName, persona);
            return NoContent();
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
    }

    private static readonly HashSet<string> ValidPersonas =
        new() { "sdr", "account_executive", "account_manager", "founder", "other" };
}
