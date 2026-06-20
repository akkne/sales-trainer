namespace Sellevate.Gamification.Features.Admin.Models;

public sealed record GamificationSettingsDto(
    int DailyXpGoal,
    int WeeklyXpGoal,
    double DialogXpMultiplier,
    int DialogWeightConfidence,
    int DialogWeightStructure,
    int DialogWeightObjection,
    int DialogWeightGoal);
