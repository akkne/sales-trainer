using Sellevate.Gamification.Features.Achievements.Models;

namespace Sellevate.Gamification.Features.Achievements.Services.Abstract;

/// <summary>
/// Reads a user's achievement wall and decides what the latest activity has just unlocked.
///
/// <para>
/// Unlocking is monotonic: an achievement already unlocked is never re-evaluated and never revoked,
/// so an evaluation may be called after every event without publishing a duplicate notification.
/// </para>
/// </summary>
public interface IAchievementService
{
    Task<IReadOnlyList<AchievementDto>> GetAchievementsForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> EvaluateAchievementsAsync(Guid userId, CancellationToken cancellationToken = default);
}
