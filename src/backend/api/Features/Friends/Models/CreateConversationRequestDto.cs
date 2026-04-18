namespace SalesTrainer.Api.Features.Friends.Models;

public sealed record CreateConversationRequestDto(
    Guid FriendUserId
);
