using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Idempotency;
using Sellevate.BuildingBlocks.Messaging;
using Sellevate.Notification.Common.Constants;
using Sellevate.Notification.Features.Notifications.Models;
using Sellevate.Notification.Features.Notifications.Services.Abstract;
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

                // Onboarding side channel: now that the replica carries the recipient's email, send a
                // one-time welcome notification (in-app + email). This lives here rather than in
                // NotificationEventConsumer because that consumer shares a group with this one on a
                // disjoint topic set — both subscribing to user.registered would split the partitions.
                // Domain-level dedup on the user id makes a Kafka replay a no-op.
                await SendWelcomeNotificationAsync(scopedServices, payload, cancellationToken);
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

    private async Task SendWelcomeNotificationAsync(
        IServiceProvider scopedServices,
        UserRegisteredEvent payload,
        CancellationToken cancellationToken)
    {
        try
        {
            var notificationService = scopedServices.GetRequiredService<INotificationService>();

            await notificationService.CreateAsync(
                new CreateNotificationRequest(
                    payload.UserId,
                    NotificationType.UserWelcome,
                    NotificationTitles.Welcome,
                    "Welcome to Sellevate! Your account is ready — start your first training session.",
                    NotificationActionRoutes.Home,
                    // Dedupe on the user id so a redelivered registration event never re-welcomes.
                    payload.UserId.ToString(),
                    SendEmail: true),
                cancellationToken);
        }
        catch (Exception exception)
        {
            // The welcome message is a best-effort side channel; never let it fail the critical
            // replica projection (which has already been persisted above).
            Logger.LogError(
                exception,
                "Failed to send welcome notification to user {UserId}",
                payload.UserId);
        }
    }
}
