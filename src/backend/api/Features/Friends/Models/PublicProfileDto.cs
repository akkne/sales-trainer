namespace SalesTrainer.Api.Features.Friends.Models;

public sealed record PublicProfileDto(
    Guid UserId,
    string DisplayName,
    string? Persona,
    int TotalXpAmount,
    int CurrentStreakDayCount,
    int AchievementCount,
    double AverageExerciseScore,
    string FriendshipStatus
);
