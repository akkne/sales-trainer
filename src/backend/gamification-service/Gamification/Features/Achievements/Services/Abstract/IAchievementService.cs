using Sellevate.Gamification.Features.Achievements.Models;

namespace Sellevate.Gamification.Features.Achievements.Services.Abstract;

public interface IAchievementService
{
    Task<IReadOnlyList<AchievementDto>> GetAchievementsForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> EvaluateAchievementsAsync(Guid userId, CancellationToken cancellationToken = default);
}
