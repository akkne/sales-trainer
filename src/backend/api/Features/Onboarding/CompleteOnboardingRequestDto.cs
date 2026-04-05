namespace SalesTrainer.Api.Features.Onboarding;

public record CompleteOnboardingRequestDto(
    string SalesType,
    string ExperienceLevel,
    List<string> SelectedSkillSlugs
);
