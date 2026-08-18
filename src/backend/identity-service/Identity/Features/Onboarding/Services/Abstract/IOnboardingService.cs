namespace Sellevate.Identity.Features.Onboarding.Services.Abstract;

/// <summary>
/// Records the answers a user gives once, when they first arrive. Idempotent: a profile already marked
/// complete is left exactly as it is, so a replayed request cannot overwrite the original answers.
/// </summary>
public interface IOnboardingService
{
    Task CompleteOnboardingForUserAsync(
        Guid userId,
        string salesType,
        string experienceLevel,
        string? persona = null,
        CancellationToken cancellationToken = default);
}
