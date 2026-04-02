namespace SalesTrainer.Api.Features.League;

public record CurrentLeagueResponseDto(
    Guid LeagueId,
    string Tier,
    DateOnly WeekStartDate,
    DateOnly WeekEndDate,
    IReadOnlyList<LeagueParticipantDto> ParticipantsByRank,
    int CurrentUserRank
);
