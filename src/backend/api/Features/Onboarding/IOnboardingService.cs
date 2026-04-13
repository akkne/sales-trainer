namespace SalesTrainer.Api.Features.Onboarding;

public interface IOnboardingService
{
    Task CompleteOnboardingForUserAsync(
        Guid userId,
        string salesType,
        string experienceLevel,
        List<string> selectedSkillSlugs,
        string? persona = null,
        CancellationToken cancellationToken = default);

    Task EnrollSkillsAsync(
        Guid userId,
        IEnumerable<string> skillSlugs,
        CancellationToken cancellationToken = default);
}
