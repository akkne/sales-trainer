namespace Sellevate.Gamification.Features.Gamification.Models;

public sealed record GamificationProgressDto(
    int CurrentStreakDayCount,
    int LongestStreakDayCount,
    int TotalXpAmount,
    int DailyXpAmount,
    int WeeklyXpAmount,
    int DailyXpGoal,
    int WeeklyXpGoal);
