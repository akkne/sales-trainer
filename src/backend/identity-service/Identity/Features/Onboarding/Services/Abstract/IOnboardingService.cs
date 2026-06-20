namespace Sellevate.Identity.Features.Onboarding.Services.Abstract;

public interface IOnboardingService
{
    Task CompleteOnboardingForUserAsync(
        Guid userId,
        string salesType,
        string experienceLevel,
        string? persona = null,
        CancellationToken cancellationToken = default);
}
