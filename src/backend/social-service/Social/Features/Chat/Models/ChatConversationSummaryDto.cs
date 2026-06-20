namespace Sellevate.Social.Features.Chat.Models;

public sealed record ChatConversationSummaryDto(
    string ConversationId,
    Guid FriendUserId,
    string FriendDisplayName,
    string? LastMessagePreview,
    DateTime? LastMessageAt
);
