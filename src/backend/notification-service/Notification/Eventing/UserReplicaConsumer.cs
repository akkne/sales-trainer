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

    /// <summary>
    /// Identity users are cross-org identities with no organization column
    /// (docs/TENANCY/TENANCY.md §4.2), so this replica projection is platform-global by design.
    /// </summary>
    protected override bool RequiresOrganization => false;

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

    /// <summary>
    /// The onboarding side channel, deliberately triggered from this consumer rather than from
    /// <see cref="NotificationEventConsumer"/>. The two share a Kafka group on disjoint topic sets,
    /// so <c>user.registered</c> can only be owned by one of them — and it has to be this one,
    /// because the welcome email needs the replica row that the caller has just written.
    ///
    /// <para>
    /// Deduped on the user id, so a Kafka replay re-upserts the replica but never re-welcomes.
    /// Best-effort by design: a failure is logged and swallowed so it can never fail the replica
    /// projection the caller has already persisted, which every later email depends on.
    /// </para>
    /// </summary>
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
                    payload.UserId.ToString(),
                    SendEmail: true),
                cancellationToken);
        }
        catch (Exception exception)
        {
            Logger.LogError(
                exception,
                "Failed to send welcome notification to user {UserId}",
                payload.UserId);
        }
    }
}
