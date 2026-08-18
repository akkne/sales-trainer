namespace Sellevate.Identity.Eventing;

/// <summary>
/// Publishes the user lifecycle facts other services keep replicas of. Implementations enqueue
/// through the outbox rather than talking to Kafka directly, so a published event and the database
/// change that caused it commit together or not at all.
/// </summary>
public interface IUserEventPublisher
{
    Task PublishRegisteredAsync(UserRegisteredEvent payload, CancellationToken cancellationToken = default);
    Task PublishUpdatedAsync(UserUpdatedEvent payload, CancellationToken cancellationToken = default);
    Task PublishDeletedAsync(UserDeletedEvent payload, CancellationToken cancellationToken = default);
    Task PublishAvatarChangedAsync(UserAvatarChangedEvent payload, CancellationToken cancellationToken = default);
}
