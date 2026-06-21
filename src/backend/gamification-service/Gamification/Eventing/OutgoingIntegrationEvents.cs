namespace Sellevate.Gamification.Eventing;

public sealed record ExperiencePointsGrantedEvent(Guid UserId, int Amount, string Source);

public sealed record AchievementUnlockedEvent(Guid UserId, string AchievementKey, string Title);

public sealed record StreakMilestoneEvent(Guid UserId, int DayCount, int BonusXp);

/// <summary>Published once per member when the weekly league rolls over.
/// <c>Outcome</c> is "promoted", "demoted" or "stayed".</summary>
public sealed record LeagueUpdatedEvent(
    Guid UserId,
    Guid LeagueId,
    string PreviousTier,
    string NewTier,
    string Outcome,
    int Rank);

public sealed record GamificationDialogWeightsUpdatedEvent(
    int Confidence,
    int Structure,
    int Objection,
    int Goal,
    double Multiplier);
