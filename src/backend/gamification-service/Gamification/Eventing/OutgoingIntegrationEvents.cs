namespace Sellevate.Gamification.Eventing;

public sealed record ExperiencePointsGrantedEvent(Guid UserId, int Amount, string Source);

public sealed record AchievementUnlockedEvent(Guid UserId, string AchievementKey, string Title);

public sealed record StreakMilestoneEvent(Guid UserId, int DayCount, int BonusXp);

public sealed record GamificationDialogWeightsUpdatedEvent(
    int Confidence,
    int Structure,
    int Objection,
    int Goal,
    double Multiplier);
