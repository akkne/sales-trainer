namespace Sellevate.Social.Eventing;

public interface ISocialEventPublisher
{
    Task PublishFriendRequestReceivedAsync(FriendRequestReceivedEvent payload, CancellationToken cancellationToken = default);
    Task PublishFriendRequestAcceptedAsync(FriendRequestAcceptedEvent payload, CancellationToken cancellationToken = default);
    Task PublishChatMessageSentAsync(ChatMessageSentEvent payload, CancellationToken cancellationToken = default);
    Task PublishChatMessageReadAsync(ChatMessageReadEvent payload, CancellationToken cancellationToken = default);
    Task PublishDiscussReplyCreatedAsync(DiscussReplyCreatedEvent payload, CancellationToken cancellationToken = default);
}
