namespace Sellevate.Social.Eventing;

public interface ISocialEventPublisher
{
    Task PublishFriendRequestReceivedAsync(FriendRequestReceivedEvent payload, CancellationToken cancellationToken = default);
    Task PublishFriendRequestAcceptedAsync(FriendRequestAcceptedEvent payload, CancellationToken cancellationToken = default);
    Task PublishChatMessageSentAsync(ChatMessageSentEvent payload, CancellationToken cancellationToken = default);
}
