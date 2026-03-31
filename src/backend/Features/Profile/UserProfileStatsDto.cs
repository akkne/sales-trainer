namespace SalesTrainer.Api.Features.Profile;

public record UserProfileStatsDto(
    string DisplayName,
    string Email,
    int CurrentStreakDayCount,
    int LongestStreakDayCount,
    int TotalXpAmount,
    int CompletedSkillCount,
    int TotalSkillCount,
    double AverageExerciseScore
);
