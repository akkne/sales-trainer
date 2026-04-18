namespace SalesTrainer.Api.Features.Friends.Models;

public sealed record FriendRequestDto(
    Guid FriendshipId,
    Guid UserId,
    string DisplayName,
    string? Persona,
    string Direction,
    DateTime CreatedAt
);
