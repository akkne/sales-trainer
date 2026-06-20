namespace Sellevate.Identity.Eventing;

/// <summary>
/// Thin domain-facing wrapper over the shared <c>IEventPublisher</c>, so Identity's
/// feature code emits <c>user.*</c> events without touching Kafka topic names or the
/// envelope directly. All events are keyed by <c>userId</c> for per-user ordering.
/// </summary>
public interface IUserEventPublisher
{
    Task PublishRegisteredAsync(UserRegisteredEvent payload, CancellationToken cancellationToken = default);
    Task PublishUpdatedAsync(UserUpdatedEvent payload, CancellationToken cancellationToken = default);
    Task PublishDeletedAsync(UserDeletedEvent payload, CancellationToken cancellationToken = default);
    Task PublishAvatarChangedAsync(UserAvatarChangedEvent payload, CancellationToken cancellationToken = default);
}
