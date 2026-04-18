namespace SalesTrainer.Api.Features.Friends.Models;

public sealed record FriendActivityDto(
    Guid UserId,
    string DisplayName,
    string ActivityType,
    string Description,
    DateTime OccurredAt
);
