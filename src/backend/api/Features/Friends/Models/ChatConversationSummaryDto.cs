namespace SalesTrainer.Api.Features.Friends.Models;

public sealed record ChatConversationSummaryDto(
    string ConversationId,
    Guid FriendUserId,
    string FriendDisplayName,
    string? LastMessagePreview,
    DateTime? LastMessageAt
);
