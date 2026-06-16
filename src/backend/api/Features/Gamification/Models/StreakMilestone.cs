namespace SalesTrainer.Api.Features.Gamification.Models;

/// <summary>
/// Admin-editable streak milestone: when a user's daily streak reaches
/// <see cref="DayCount"/>, they receive a one-off bonus of <see cref="XpReward"/> XP.
/// </summary>
public sealed class StreakMilestone
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Streak length (in consecutive days) that triggers the bonus.</summary>
    public int DayCount { get; set; }

    /// <summary>Bonus XP awarded when the streak first reaches <see cref="DayCount"/>.</summary>
    public int XpReward { get; set; }
}
