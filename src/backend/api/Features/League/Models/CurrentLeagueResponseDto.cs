namespace SalesTrainer.Api.Features.League.Models;

public record CurrentLeagueResponseDto(
    Guid LeagueId,
    string Tier,
    string TierName,
    string TierColor,
    DateOnly WeekStartDate,
    DateOnly WeekEndDate,
    DateTimeOffset PeriodEndsAt,
    IReadOnlyList<LeagueParticipantDto> ParticipantsByRank,
    int CurrentUserRank,
    string? PreviousWeekOutcome,
    int PromotionZoneSize,
    int DemotionZoneSize,
    int MaximumLeagueParticipantCount
);
