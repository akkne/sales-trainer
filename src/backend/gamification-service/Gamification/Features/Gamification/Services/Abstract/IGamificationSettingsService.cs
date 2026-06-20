using Sellevate.Gamification.Features.Gamification.Models;

namespace Sellevate.Gamification.Features.Gamification.Services.Abstract;

public interface IGamificationSettingsService
{
    Task<GamificationSettings> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<int> GetExerciseBaseExperiencePointsAsync(string exerciseType, CancellationToken cancellationToken = default);

    Task<int> GetStreakBonusExperiencePointsAsync(int streakDayCount, CancellationToken cancellationToken = default);
}
