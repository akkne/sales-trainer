namespace Sellevate.Gamification.Features.Achievements.Models;

public sealed class UserAchievement
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid AchievementId { get; set; }
    public DateTime UnlockedAt { get; set; }
}
