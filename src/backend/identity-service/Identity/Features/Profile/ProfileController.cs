using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.Identity.Common.Constants;
using Sellevate.Identity.Features.Profile.Models;
using Sellevate.Identity.Features.Profile.Services.Abstract;

namespace Sellevate.Identity.Features.Profile;

/// <summary>
/// The caller's own profile. Every route resolves the user from the token and never from the request, so
/// there is no way to address somebody else's profile through this controller.
/// </summary>
[ApiController]
[Route("profile")]
[Authorize]
public sealed class ProfileController(IProfileService profileService) : ControllerBase
{
    private const int DisplayNameMaximumLength = 100;

    [HttpGet]
    public async Task<ActionResult<UserProfileStatsDto>> GetProfileStats()
    {
        if (ResolveUserId() is not { } userId)
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
        if (ResolveUserId() is not { } userId)
        {
            return Unauthorized();
        }

        if (!ValidPersonas.Contains(request.Persona))
        {
            return BadRequest(new { message = InvalidPersonaMessage });
        }

        await profileService.UpdatePersonaForUserAsync(userId, request.Persona);
        return NoContent();
    }

    [HttpPut]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequestDto request)
    {
        if (ResolveUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var displayName = request.DisplayName?.Trim() ?? "";

        if (displayName.Length == 0)
        {
            return BadRequest(new { message = "Display name is required." });
        }

        if (displayName.Length > DisplayNameMaximumLength)
        {
            return BadRequest(new
            {
                message = $"Display name must be {DisplayNameMaximumLength} characters or fewer."
            });
        }

        var persona = string.IsNullOrWhiteSpace(request.Persona) ? null : request.Persona;

        if (persona is not null && !ValidPersonas.Contains(persona))
        {
            return BadRequest(new { message = InvalidPersonaMessage });
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

    private const string InvalidPersonaMessage = "Invalid persona value.";

    private static readonly HashSet<string> ValidPersonas =
        new() { "sdr", "account_executive", "account_manager", "founder", "other" };

    /// <summary>
    /// The caller's user id, or <see langword="null"/> when the token carries none. Reads
    /// <c>ClaimTypes.NameIdentifier</c> first and falls back to the raw <c>sub</c> spelling, because a
    /// principal built by the JWT handler carries the mapped URI while one forwarded by the gateway keeps
    /// the wire name.
    /// </summary>
    private Guid? ResolveUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(ClaimTypeNames.Subject);

        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
