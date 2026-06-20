using System.Collections.Concurrent;
using Sellevate.Identity.Eventing;

namespace Sellevate.Identity.Tests.Helpers;

public sealed class RecordingUserEventPublisher : IUserEventPublisher
{
    public ConcurrentQueue<UserRegisteredEvent> Registered { get; } = new();
    public ConcurrentQueue<UserUpdatedEvent> Updated { get; } = new();
    public ConcurrentQueue<UserDeletedEvent> Deleted { get; } = new();
    public ConcurrentQueue<UserAvatarChangedEvent> AvatarChanged { get; } = new();

    public Task PublishRegisteredAsync(UserRegisteredEvent payload, CancellationToken cancellationToken = default)
    {
        Registered.Enqueue(payload);
        return Task.CompletedTask;
    }

    public Task PublishUpdatedAsync(UserUpdatedEvent payload, CancellationToken cancellationToken = default)
    {
        Updated.Enqueue(payload);
        return Task.CompletedTask;
    }

    public Task PublishDeletedAsync(UserDeletedEvent payload, CancellationToken cancellationToken = default)
    {
        Deleted.Enqueue(payload);
        return Task.CompletedTask;
    }

    public Task PublishAvatarChangedAsync(UserAvatarChangedEvent payload, CancellationToken cancellationToken = default)
    {
        AvatarChanged.Enqueue(payload);
        return Task.CompletedTask;
    }
}
