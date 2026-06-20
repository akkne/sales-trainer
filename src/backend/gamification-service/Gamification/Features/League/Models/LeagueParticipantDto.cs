namespace Sellevate.Gamification.Features.League.Models;

public sealed record LeagueParticipantDto(
    string UserId,
    string DisplayName,
    int WeeklyXpAmount,
    int Rank,
    bool IsCurrentUser,
    string AvatarUrl);
