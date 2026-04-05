using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SalesTrainer.Api.Features.Onboarding;

[ApiController]
[Route("onboarding")]
[Authorize]
public class OnboardingController(OnboardingService onboardingService) : ControllerBase
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
            onboardingRequest.SelectedSkillSlugs,
            onboardingRequest.Persona);

        return NoContent();
    }
}
