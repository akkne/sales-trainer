using SalesTrainer.Api.Features.Gamification.Models;

namespace SalesTrainer.Api.Features.Gamification.Services.Abstract;

/// <summary>
/// Central access point for the admin-editable XP/gamification configuration that
/// used to live as hardcoded constants across the codebase.
/// </summary>
public interface IGamificationService
{
    /// <summary>Loads the singleton settings row, creating it with defaults if absent.</summary>
    Task<GamificationSettings> GetSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>Base XP awarded for a correct/passed answer of the given exercise type.</summary>
    Task<int> GetExerciseBaseXpAsync(string exerciseType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bonus XP for reaching exactly <paramref name="streakDayCount"/> consecutive days,
    /// or 0 if that day count is not a configured milestone.
    /// </summary>
    Task<int> GetStreakBonusXpAsync(int streakDayCount, CancellationToken cancellationToken = default);
}
