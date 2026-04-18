namespace SalesTrainer.Api.Features.Friends.Models;

public sealed record FriendLeaderboardEntryDto(
    Guid UserId,
    string DisplayName,
    int TotalXpAmount,
    int Rank,
    bool IsCurrentUser
);
