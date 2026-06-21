namespace Sellevate.Gamification.Features.Gamification.Services.Abstract;

public interface IExperiencePointsGrantService
{
    Task GrantAsync(Guid userId, int amount, string source, DateTime? earnedAt = null, Guid? sourceEventId = null, CancellationToken cancellationToken = default);
}
