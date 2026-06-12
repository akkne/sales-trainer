namespace SalesTrainer.Api.Features.League.Models;

public record LeagueParticipantDto(
    string UserId,
    string DisplayName,
    int WeeklyXpAmount,
    int Rank,
    bool IsCurrentUser,
    string AvatarUrl
);
