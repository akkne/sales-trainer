using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Messaging;

namespace Sellevate.Identity.Eventing;

internal sealed class KafkaUserEventPublisher(IEventPublisher eventPublisher) : IUserEventPublisher
{
    public Task PublishRegisteredAsync(UserRegisteredEvent payload, CancellationToken cancellationToken = default) =>
        eventPublisher.PublishAsync(
            Topics.UserRegistered, payload.UserId.ToString(), Topics.UserRegistered, payload, cancellationToken: cancellationToken);

    public Task PublishUpdatedAsync(UserUpdatedEvent payload, CancellationToken cancellationToken = default) =>
        eventPublisher.PublishAsync(
            Topics.UserUpdated, payload.UserId.ToString(), Topics.UserUpdated, payload, cancellationToken: cancellationToken);

    public Task PublishDeletedAsync(UserDeletedEvent payload, CancellationToken cancellationToken = default) =>
        eventPublisher.PublishAsync(
            Topics.UserDeleted, payload.UserId.ToString(), Topics.UserDeleted, payload, cancellationToken: cancellationToken);

    public Task PublishAvatarChangedAsync(UserAvatarChangedEvent payload, CancellationToken cancellationToken = default) =>
        eventPublisher.PublishAsync(
            Topics.UserAvatarChanged, payload.UserId.ToString(), Topics.UserAvatarChanged, payload, cancellationToken: cancellationToken);
}
