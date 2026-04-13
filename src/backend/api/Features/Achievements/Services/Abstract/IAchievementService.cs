using SalesTrainer.Api.Features.Achievements.Models;

namespace SalesTrainer.Api.Features.Achievements.Services.Abstract;

public interface IAchievementService
{
    Task<IReadOnlyList<AchievementDto>> GetAchievementsForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> EvaluateAchievementsAfterSubmitAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
