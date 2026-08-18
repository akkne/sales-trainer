using Sellevate.Gamification.Features.Gamification.Models;

namespace Sellevate.Gamification.Features.Gamification.Services.Abstract;

/// <summary>
/// Resolves the installation-wide gamification tuning — goals, dialog weights, per-exercise-type
/// rewards and streak milestone bonuses — from the database, falling back to built-in defaults when
/// an administrator has configured none.
///
/// <para>
/// A reward or bonus lookup that matches nothing returns zero rather than a default value: the
/// caller must be able to tell "no bonus applies" from "the platform default applies", and only the
/// per-exercise base reward has a meaningful default.
/// </para>
/// </summary>
public interface IGamificationSettingsService
{
    Task<GamificationSettings> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<int> GetExerciseBaseExperiencePointsAsync(string exerciseType, CancellationToken cancellationToken = default);

    Task<int> GetStreakBonusExperiencePointsAsync(int streakDayCount, CancellationToken cancellationToken = default);
}
