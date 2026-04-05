namespace SalesTrainer.Api.Features.Achievements;

public record AchievementDto(
    Guid AchievementId,
    string Key,
    string Title,
    string Description,
    string IconEmoji,
    bool IsUnlocked,
    DateTime? UnlockedAt
);
