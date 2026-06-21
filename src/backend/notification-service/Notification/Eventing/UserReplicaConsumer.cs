using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Idempotency;
using Sellevate.BuildingBlocks.Messaging;
using Sellevate.Notification.Features.Users;

namespace Sellevate.Notification.Eventing;

/// <summary>
/// Projects Identity's user lifecycle events into the local Redis user replica so the service can
/// resolve a recipient's email/display name when sending email notifications. Subscribes to a
/// topic set disjoint from <see cref="NotificationEventConsumer"/>, so the two coexist in the same
/// consumer group without stealing each other's partitions.
/// </summary>
internal sealed class UserReplicaConsumer : KafkaConsumerBackgroundService
{
    public UserReplicaConsumer(
        IOptions<KafkaSettings> settings,
        IServiceScopeFactory scopeFactory,
        IIdempotencyStore idempotencyStore,
        ILogger<UserReplicaConsumer> logger)
        : base(settings, scopeFactory, idempotencyStore, logger)
    {
    }

    protected override IReadOnlyCollection<string> Topics =>
    [
        BuildingBlocks.Eventing.Topics.UserRegistered,
        BuildingBlocks.Eventing.Topics.UserUpdated,
        BuildingBlocks.Eventing.Topics.UserDeleted,
    ];

    protected override async Task HandleAsync(
        EventEnvelope envelope,
        IServiceProvider scopedServices,
        CancellationToken cancellationToken)
    {
        var directory = scopedServices.GetRequiredService<IUserDirectory>();

        switch (envelope.Type)
        {
            case BuildingBlocks.Eventing.Topics.UserRegistered:
            {
                var payload = envelope.DataAs<UserRegisteredEvent>();
                if (payload is null || string.IsNullOrWhiteSpace(payload.Email))
                {
                    return;
                }

                await directory.UpsertAsync(
                    new UserProfile(payload.UserId, payload.Email, payload.DisplayName ?? string.Empty),
                    cancellationToken);
                break;
            }

            case BuildingBlocks.Eventing.Topics.UserUpdated:
            {
                var payload = envelope.DataAs<UserUpdatedEvent>();
                if (payload is null)
                {
                    return;
                }

                await directory.UpdateDisplayNameAsync(
                    payload.UserId, payload.DisplayName ?? string.Empty, cancellationToken);
                break;
            }

            case BuildingBlocks.Eventing.Topics.UserDeleted:
            {
                var payload = envelope.DataAs<UserDeletedEvent>();
                if (payload is null)
                {
                    return;
                }

                await directory.RemoveAsync(payload.UserId, cancellationToken);
                break;
            }
        }
    }
}
