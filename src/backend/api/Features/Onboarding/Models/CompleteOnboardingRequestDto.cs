namespace SalesTrainer.Api.Features.Onboarding.Models;

public record CompleteOnboardingRequestDto(
    string SalesType,
    string ExperienceLevel,
    List<string> SelectedSkillSlugs,
    string? Persona = null
);
