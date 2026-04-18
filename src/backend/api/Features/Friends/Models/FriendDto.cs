namespace SalesTrainer.Api.Features.Friends.Models;

public sealed record FriendDto(
    Guid UserId,
    string DisplayName,
    string? Persona,
    int TotalXpAmount,
    int CurrentStreakDayCount,
    int AchievementCount
);
