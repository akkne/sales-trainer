using Sellevate.Social.Eventing;

namespace Sellevate.Social.Tests.Helpers;

internal sealed class RecordingSocialEventPublisher : ISocialEventPublisher
{
    public List<FriendRequestReceivedEvent> FriendRequestReceivedEvents { get; } = [];
    public List<FriendRequestAcceptedEvent> FriendRequestAcceptedEvents { get; } = [];
    public List<ChatMessageSentEvent> ChatMessageSentEvents { get; } = [];

    public Task PublishFriendRequestReceivedAsync(FriendRequestReceivedEvent payload, CancellationToken cancellationToken = default)
    {
        FriendRequestReceivedEvents.Add(payload);
        return Task.CompletedTask;
    }

    public Task PublishFriendRequestAcceptedAsync(FriendRequestAcceptedEvent payload, CancellationToken cancellationToken = default)
    {
        FriendRequestAcceptedEvents.Add(payload);
        return Task.CompletedTask;
    }

    public Task PublishChatMessageSentAsync(ChatMessageSentEvent payload, CancellationToken cancellationToken = default)
    {
        ChatMessageSentEvents.Add(payload);
        return Task.CompletedTask;
    }
}
