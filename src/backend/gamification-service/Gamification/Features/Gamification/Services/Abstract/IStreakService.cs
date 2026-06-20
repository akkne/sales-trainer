namespace Sellevate.Gamification.Features.Gamification.Services.Abstract;

public interface IStreakService
{
    Task RegisterActivityAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<int> GetCurrentStreakDayCountAsync(Guid userId, CancellationToken cancellationToken = default);
}
