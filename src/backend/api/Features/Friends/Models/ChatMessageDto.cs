namespace SalesTrainer.Api.Features.Friends.Models;

public sealed record ChatMessageDto(
    string Id,
    Guid SenderId,
    string Content,
    DateTime SentAt,
    bool IsOwn
);
