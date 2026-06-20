namespace Sellevate.Gamification.Features.Gamification.Models;

public sealed class StreakMilestone
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int DayCount { get; set; }
    public int XpReward { get; set; }
}
