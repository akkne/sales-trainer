using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Outbox;

namespace Sellevate.Identity.Eventing;

internal sealed class KafkaUserEventPublisher(IOutboxWriter outboxWriter) : IUserEventPublisher
{
    public Task PublishRegisteredAsync(UserRegisteredEvent payload, CancellationToken cancellationToken = default)
    {
        outboxWriter.Enqueue(Topics.UserRegistered, payload.UserId.ToString(), Topics.UserRegistered, payload);
        return Task.CompletedTask;
    }

    public Task PublishUpdatedAsync(UserUpdatedEvent payload, CancellationToken cancellationToken = default)
    {
        outboxWriter.Enqueue(Topics.UserUpdated, payload.UserId.ToString(), Topics.UserUpdated, payload);
        return Task.CompletedTask;
    }

    public Task PublishDeletedAsync(UserDeletedEvent payload, CancellationToken cancellationToken = default)
    {
        outboxWriter.Enqueue(Topics.UserDeleted, payload.UserId.ToString(), Topics.UserDeleted, payload);
        return Task.CompletedTask;
    }

    public Task PublishAvatarChangedAsync(UserAvatarChangedEvent payload, CancellationToken cancellationToken = default)
    {
        outboxWriter.Enqueue(Topics.UserAvatarChanged, payload.UserId.ToString(), Topics.UserAvatarChanged, payload);
        return Task.CompletedTask;
    }
}
