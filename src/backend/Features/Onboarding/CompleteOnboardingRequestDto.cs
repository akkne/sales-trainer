namespace SalesTrainer.Api.Features.Onboarding;

public record CompleteOnboardingRequestDto(
    string SalesType,
    string ExperienceLevel,
    string Goal
);
