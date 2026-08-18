using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.Identity.Common.Constants;
using Sellevate.Identity.Features.Onboarding.Models;
using Sellevate.Identity.Features.Onboarding.Services.Abstract;

namespace Sellevate.Identity.Features.Onboarding;

/// <summary>
/// Records the caller's onboarding answers. The user is resolved from the token, so a caller can only
/// complete their own onboarding.
/// </summary>
[ApiController]
[Route("onboarding")]
[Authorize]
public sealed class OnboardingController(IOnboardingService onboardingService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CompleteOnboarding(
        [FromBody] CompleteOnboardingRequestDto onboardingRequest)
    {
        if (ResolveUserId() is not { } userId)
        {
            return Unauthorized();
        }

        await onboardingService.CompleteOnboardingForUserAsync(
            userId,
            onboardingRequest.SalesType,
            onboardingRequest.ExperienceLevel,
            onboardingRequest.Persona);

        return NoContent();
    }

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
