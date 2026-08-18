using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Outbox;

namespace Sellevate.Identity.Eventing;

/// <summary>
/// Maps each user event onto its topic and stages it in the outbox. The methods are synchronous work
/// behind a <see cref="Task"/>-returning signature on purpose: nothing leaves the process here, so
/// there is nothing to await, and the interface stays awaitable for a future transport that does.
/// The user id is the partition key, which keeps every fact about one user in order.
/// </summary>
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
