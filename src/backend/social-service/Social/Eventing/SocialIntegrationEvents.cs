namespace Sellevate.Social.Eventing;

public sealed record FriendRequestReceivedEvent(
    Guid RecipientId,
    string RequesterName,
    Guid? RequesterId,
    Guid? FriendshipId);

public sealed record FriendRequestAcceptedEvent(
    Guid RecipientId,
    string AccepterName,
    Guid? AccepterId);

public sealed record ChatMessageSentEvent(
    Guid RecipientId,
    string SenderName,
    string Preview,
    Guid? ConversationId);

public sealed record ChatMessageReadEvent(
    Guid ReaderUserId,
    Guid? ConversationId,
    DateTime ReadAt);

public sealed record DiscussReplyCreatedEvent(
    Guid RecipientId,
    Guid ReplyAuthorId,
    string ReplyAuthorName,
    Guid ThreadId,
    string ThreadTitle,
    Guid ReplyId,
    string Preview);

public sealed record UserRegisteredEvent(Guid UserId, string Email, string DisplayName, string? AvatarKey);

public sealed record UserUpdatedEvent(Guid UserId, string DisplayName, string? AvatarKey);

public sealed record UserDeletedEvent(Guid UserId);
