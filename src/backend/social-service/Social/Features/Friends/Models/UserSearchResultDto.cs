namespace Sellevate.Social.Features.Friends.Models;

public sealed record UserSearchResultDto(
    Guid UserId,
    string DisplayName,
    string? Persona,
    string FriendshipStatus,
    string AvatarUrl,
    Guid? FriendshipId
);
