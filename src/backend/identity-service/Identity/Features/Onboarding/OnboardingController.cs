using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.Identity.Features.Onboarding.Models;
using Sellevate.Identity.Features.Onboarding.Services.Abstract;

namespace Sellevate.Identity.Features.Onboarding;

[ApiController]
[Route("onboarding")]
[Authorize]
public class OnboardingController(IOnboardingService onboardingService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CompleteOnboarding(
        [FromBody] CompleteOnboardingRequestDto onboardingRequest)
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(rawUserId, out var userId))
            return Unauthorized();

        await onboardingService.CompleteOnboardingForUserAsync(
            userId,
            onboardingRequest.SalesType,
            onboardingRequest.ExperienceLevel,
            onboardingRequest.Persona);

        return NoContent();
    }
}
