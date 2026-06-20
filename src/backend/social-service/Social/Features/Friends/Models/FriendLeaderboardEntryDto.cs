namespace Sellevate.Social.Features.Friends.Models;

public sealed record FriendLeaderboardEntryDto(
    Guid UserId,
    string DisplayName,
    int TotalXpAmount,
    int Rank,
    bool IsCurrentUser,
    string AvatarUrl
);
