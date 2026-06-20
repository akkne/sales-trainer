namespace Sellevate.Identity.Eventing;

public interface IUserEventPublisher
{
    Task PublishRegisteredAsync(UserRegisteredEvent payload, CancellationToken cancellationToken = default);
    Task PublishUpdatedAsync(UserUpdatedEvent payload, CancellationToken cancellationToken = default);
    Task PublishDeletedAsync(UserDeletedEvent payload, CancellationToken cancellationToken = default);
    Task PublishAvatarChangedAsync(UserAvatarChangedEvent payload, CancellationToken cancellationToken = default);
}
