namespace SalesTrainer.Api.Features.League.Models;

public record CurrentLeagueResponseDto(
    Guid LeagueId,
    string Tier,
    DateOnly WeekStartDate,
    DateOnly WeekEndDate,
    IReadOnlyList<LeagueParticipantDto> ParticipantsByRank,
    int CurrentUserRank,
    string? PreviousWeekOutcome,
    int PromotionZoneSize,
    int DemotionZoneSize,
    int MaximumLeagueParticipantCount
);
