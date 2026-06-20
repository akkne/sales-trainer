namespace Sellevate.Gamification.Features.Achievements.Models;

public sealed record AchievementDto(
    Guid AchievementId,
    string Key,
    string Title,
    string Description,
    string IconEmoji,
    bool IsUnlocked,
    DateTime? UnlockedAt);
