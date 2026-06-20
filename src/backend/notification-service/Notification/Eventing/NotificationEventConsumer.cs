using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Idempotency;
using Sellevate.BuildingBlocks.Messaging;
using Sellevate.Notification.Features.Notifications.Services.Abstract;

namespace Sellevate.Notification.Eventing;

internal sealed class NotificationEventConsumer : KafkaConsumerBackgroundService
{
    private readonly INotificationEventMapper _eventMapper;

    public NotificationEventConsumer(
        IOptions<KafkaSettings> settings,
        IServiceScopeFactory scopeFactory,
        IIdempotencyStore idempotencyStore,
        INotificationEventMapper eventMapper,
        ILogger<NotificationEventConsumer> logger)
        : base(settings, scopeFactory, idempotencyStore, logger)
    {
        ArgumentNullException.ThrowIfNull(eventMapper);
        _eventMapper = eventMapper;
    }

    protected override IReadOnlyCollection<string> Topics =>
    [
        BuildingBlocks.Eventing.Topics.AchievementUnlocked,
        BuildingBlocks.Eventing.Topics.StreakMilestone,
        BuildingBlocks.Eventing.Topics.FriendRequestReceived,
        BuildingBlocks.Eventing.Topics.FriendRequestAccepted,
        BuildingBlocks.Eventing.Topics.ChatMessageSent,
    ];

    protected override async Task HandleAsync(
        EventEnvelope envelope,
        IServiceProvider scopedServices,
        CancellationToken cancellationToken)
    {
        var createRequest = _eventMapper.Map(envelope);
        if (createRequest is null)
        {
            Logger.LogWarning(
                "Skipping unmappable event {EventId} of type {Type}",
                envelope.EventId, envelope.Type);
            return;
        }

        var notificationService = scopedServices.GetRequiredService<INotificationService>();
        await notificationService.CreateAsync(createRequest, cancellationToken);
    }
}
