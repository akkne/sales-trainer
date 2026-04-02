namespace SalesTrainer.Api.Features.League;

public record LeagueParticipantDto(
    string UserId,
    string DisplayName,
    int WeeklyXpAmount,
    int Rank,
    bool IsCurrentUser
);
