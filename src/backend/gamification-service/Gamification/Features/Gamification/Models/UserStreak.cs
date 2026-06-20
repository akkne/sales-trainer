namespace Sellevate.Gamification.Features.Gamification.Models;

public sealed class UserStreak
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int CurrentStreakDayCount { get; set; }
    public int LongestStreakDayCount { get; set; }
    public DateOnly? LastActivityDate { get; set; }
}
