namespace Sellevate.Social.Eventing;

/// <summary>
/// The five integration events social-service emits, all consumed by notification-service. Publishing
/// is not best-effort: every implementation stamps the caller's organization onto the envelope and
/// refuses to publish without one, because a notification with no organization has no inbox to land
/// in and would dead-letter downstream.
///
/// <para>
/// Callers publish <em>after</em> the transaction that produced the event has committed, so an event
/// always describes a state the database already holds.
/// </para>
/// </summary>
public interface ISocialEventPublisher
{
    Task PublishFriendRequestReceivedAsync(FriendRequestReceivedEvent payload, CancellationToken cancellationToken = default);
    Task PublishFriendRequestAcceptedAsync(FriendRequestAcceptedEvent payload, CancellationToken cancellationToken = default);
    Task PublishChatMessageSentAsync(ChatMessageSentEvent payload, CancellationToken cancellationToken = default);
    Task PublishChatMessageReadAsync(ChatMessageReadEvent payload, CancellationToken cancellationToken = default);
    Task PublishDiscussReplyCreatedAsync(DiscussReplyCreatedEvent payload, CancellationToken cancellationToken = default);
}
