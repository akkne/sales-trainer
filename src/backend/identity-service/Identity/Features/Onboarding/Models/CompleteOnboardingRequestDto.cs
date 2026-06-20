namespace Sellevate.Identity.Features.Onboarding.Models;

public record CompleteOnboardingRequestDto(
    string SalesType,
    string ExperienceLevel,
    string? Persona = null
);
