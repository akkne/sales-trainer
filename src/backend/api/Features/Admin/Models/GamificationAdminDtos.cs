namespace SalesTrainer.Api.Features.Admin.Models;

// --- Settings (singleton: daily/weekly goals + dialog scoring) ---

public sealed record GamificationSettingsDto(
    int DailyXpGoal,
    int WeeklyXpGoal,
    double DialogXpMultiplier,
    int DialogWeightConfidence,
    int DialogWeightStructure,
    int DialogWeightObjection,
    int DialogWeightGoal);

public sealed record UpdateGamificationSettingsRequestDto(
    int DailyXpGoal,
    int WeeklyXpGoal,
    double DialogXpMultiplier,
    int DialogWeightConfidence,
    int DialogWeightStructure,
    int DialogWeightObjection,
    int DialogWeightGoal);

// --- Per-exercise-type base XP ---

public sealed record ExerciseTypeRewardDto(Guid Id, string ExerciseType, int BaseXpReward);

public sealed record UpdateExerciseTypeRewardRequestDto(int BaseXpReward);

// --- Streak milestones ---

public sealed record StreakMilestoneDto(Guid Id, int DayCount, int XpReward);

public sealed record SaveStreakMilestoneRequestDto(int DayCount, int XpReward);
