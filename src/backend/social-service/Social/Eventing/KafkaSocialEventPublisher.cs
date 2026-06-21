using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Messaging;

namespace Sellevate.Social.Eventing;

internal sealed class KafkaSocialEventPublisher(IEventPublisher eventPublisher) : ISocialEventPublisher
{
    public Task PublishFriendRequestReceivedAsync(FriendRequestReceivedEvent payload, CancellationToken cancellationToken = default) =>
        eventPublisher.PublishAsync(
            Topics.FriendRequestReceived, payload.RecipientId.ToString(), Topics.FriendRequestReceived, payload, cancellationToken: cancellationToken);

    public Task PublishFriendRequestAcceptedAsync(FriendRequestAcceptedEvent payload, CancellationToken cancellationToken = default) =>
        eventPublisher.PublishAsync(
            Topics.FriendRequestAccepted, payload.RecipientId.ToString(), Topics.FriendRequestAccepted, payload, cancellationToken: cancellationToken);

    public Task PublishChatMessageSentAsync(ChatMessageSentEvent payload, CancellationToken cancellationToken = default) =>
        eventPublisher.PublishAsync(
            Topics.ChatMessageSent, payload.RecipientId.ToString(), Topics.ChatMessageSent, payload, cancellationToken: cancellationToken);

    public Task PublishChatMessageReadAsync(ChatMessageReadEvent payload, CancellationToken cancellationToken = default) =>
        eventPublisher.PublishAsync(
            Topics.ChatMessageRead, payload.ReaderUserId.ToString(), Topics.ChatMessageRead, payload, cancellationToken: cancellationToken);

    public Task PublishDiscussReplyCreatedAsync(DiscussReplyCreatedEvent payload, CancellationToken cancellationToken = default) =>
        eventPublisher.PublishAsync(
            Topics.DiscussReplyCreated, payload.RecipientId.ToString(), Topics.DiscussReplyCreated, payload, cancellationToken: cancellationToken);
}
