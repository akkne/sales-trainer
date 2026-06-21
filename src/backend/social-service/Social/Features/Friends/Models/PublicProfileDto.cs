namespace Sellevate.Social.Features.Friends.Models;

public sealed record PublicProfileDto(
    Guid UserId,
    string DisplayName,
    string? Persona,
    int TotalXpAmount,
    int CurrentStreakDayCount,
    int AchievementCount,
    double AverageExerciseScore,
    string FriendshipStatus,
    string AvatarUrl,
    Guid? FriendshipId
);
