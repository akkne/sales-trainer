using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Idempotency;
using Sellevate.BuildingBlocks.Messaging;
using Sellevate.Notification.Features.Notifications.Emails.Delayed;
using Sellevate.Notification.Features.Notifications.Services.Abstract;
using Sellevate.Notification.Infrastructure.Configuration;

namespace Sellevate.Notification.Eventing;

internal sealed class NotificationEventConsumer : KafkaConsumerBackgroundService
{
    private readonly INotificationEventMapper _eventMapper;
    private readonly IDelayedChatEmailScheduler _delayedChatEmailScheduler;
    private readonly NotificationEmailConfiguration _emailConfiguration;

    public NotificationEventConsumer(
        IOptions<KafkaSettings> settings,
        IServiceScopeFactory scopeFactory,
        IIdempotencyStore idempotencyStore,
        INotificationEventMapper eventMapper,
        IDelayedChatEmailScheduler delayedChatEmailScheduler,
        IOptions<NotificationEmailConfiguration> emailConfiguration,
        ILogger<NotificationEventConsumer> logger)
        : base(settings, scopeFactory, idempotencyStore, logger)
    {
        ArgumentNullException.ThrowIfNull(eventMapper);
        ArgumentNullException.ThrowIfNull(delayedChatEmailScheduler);
        ArgumentNullException.ThrowIfNull(emailConfiguration);
        _eventMapper = eventMapper;
        _delayedChatEmailScheduler = delayedChatEmailScheduler;
        _emailConfiguration = emailConfiguration.Value;
    }

    protected override IReadOnlyCollection<string> Topics =>
    [
        BuildingBlocks.Eventing.Topics.AchievementUnlocked,
        BuildingBlocks.Eventing.Topics.StreakMilestone,
        BuildingBlocks.Eventing.Topics.FriendRequestReceived,
        BuildingBlocks.Eventing.Topics.FriendRequestAccepted,
        BuildingBlocks.Eventing.Topics.ChatMessageSent,
        BuildingBlocks.Eventing.Topics.ChatMessageRead,
        BuildingBlocks.Eventing.Topics.DiscussReplyCreated,
        BuildingBlocks.Eventing.Topics.LeagueUpdated,
        BuildingBlocks.Eventing.Topics.CompanyFollowUpDue,
    ];

    protected override async Task HandleAsync(
        EventEnvelope envelope,
        IServiceProvider scopedServices,
        CancellationToken cancellationToken)
    {
        // A read receipt carries no notification — it only cancels a pending unread-chat email.
        if (envelope.Type == BuildingBlocks.Eventing.Topics.ChatMessageRead)
        {
            await HandleChatMessageReadAsync(envelope, cancellationToken);
            return;
        }

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

        // A direct message is emailed only if it is still unread after the grace period, so we
        // queue a delayed check here rather than emailing at creation time.
        if (envelope.Type == BuildingBlocks.Eventing.Topics.ChatMessageSent)
        {
            await ScheduleUnreadChatEmailAsync(envelope, createRequest, cancellationToken);
        }
    }

    private async Task HandleChatMessageReadAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        var payload = envelope.DataAs<ChatMessageReadEvent>();
        if (payload is null || payload.ReaderUserId == Guid.Empty)
        {
            return;
        }

        await _delayedChatEmailScheduler.MarkConversationReadAsync(
            payload.ReaderUserId, payload.ConversationId, payload.ReadAt, cancellationToken);
    }

    private async Task ScheduleUnreadChatEmailAsync(
        EventEnvelope envelope,
        Features.Notifications.Models.CreateNotificationRequest createRequest,
        CancellationToken cancellationToken)
    {
        var messageSentAt = envelope.OccurredAt.UtcDateTime;
        var dueAt = messageSentAt.AddMinutes(Math.Max(0, _emailConfiguration.ChatUnreadDelayMinutes));
        var conversationId = Guid.TryParse(createRequest.RelatedEntityId, out var parsed) ? parsed : (Guid?)null;

        await _delayedChatEmailScheduler.ScheduleAsync(
            new PendingChatEmail(
                createRequest.RecipientUserId,
                createRequest.Body,
                createRequest.ActionUrl,
                conversationId,
                messageSentAt,
                dueAt),
            cancellationToken);
    }
}
