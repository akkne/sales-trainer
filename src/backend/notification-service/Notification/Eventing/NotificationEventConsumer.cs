using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Idempotency;
using Sellevate.BuildingBlocks.Messaging;
using Sellevate.Notification.Features.Notifications.Emails.Delayed;
using Sellevate.Notification.Features.Notifications.Services.Abstract;
using Sellevate.Notification.Infrastructure.Configuration;

namespace Sellevate.Notification.Eventing;

/// <summary>
/// Consumes every domain event that can become a notification, maps it through
/// <see cref="INotificationEventMapper"/> and stores the result. An event the mapper declines is
/// logged and acknowledged, not retried: the mapper's <c>null</c> means "this must not become a
/// notification", which redelivery would never change.
///
/// <para>
/// Two topics are not notifications at all and are handled here rather than in the mapper:
/// <c>chat.message.sent</c> additionally arms the delayed unread-chat email, and
/// <c>chat.message.read</c> only cancels one.
/// </para>
/// </summary>
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
        BuildingBlocks.Eventing.Topics.CompanyFollowUpDue,
        BuildingBlocks.Eventing.Topics.AssignmentIssued,
        BuildingBlocks.Eventing.Topics.AssignmentDeadlineApproaching,
        BuildingBlocks.Eventing.Topics.AssignmentReminder,
        BuildingBlocks.Eventing.Topics.AssignmentDeadlineDigest,
        BuildingBlocks.Eventing.Topics.DialogReviewCommented,
        BuildingBlocks.Eventing.Topics.DialogReviewResolved,
        BuildingBlocks.Eventing.Topics.DialogReviewDisputed,
    ];

    /// <summary>
    /// Phase 40.13 leaves <c>RequiresOrganization</c> at its inherited <c>true</c>, and that is a
    /// decision rather than an omission: every topic here produces a notification in one
    /// organization's inbox, so an envelope with no tenant has no correct destination. The base
    /// class turns that into a normal handler failure, which retries and then dead-letters —
    /// visible, unlike a notification quietly filed under a blank organization.
    /// </summary>
    protected override async Task HandleAsync(
        EventEnvelope envelope,
        IServiceProvider scopedServices,
        CancellationToken cancellationToken)
    {
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

        if (envelope.Type == BuildingBlocks.Eventing.Topics.ChatMessageSent)
        {
            await ScheduleUnreadChatEmailAsync(envelope, createRequest, cancellationToken);
        }
    }

    /// <summary>
    /// Records the read watermark that suppresses a still-pending unread-chat email. The organization
    /// comes from the envelope rather than from ambient state — a consumer's tenant is a property of
    /// the message in its hand — and cannot be empty here because the base class has already rejected
    /// an envelope without one.
    /// </summary>
    private async Task HandleChatMessageReadAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        var payload = envelope.DataAs<ChatMessageReadEvent>();
        if (payload is null || payload.ReaderUserId == Guid.Empty)
        {
            return;
        }

        await _delayedChatEmailScheduler.MarkConversationReadAsync(
            envelope.OrganizationId!.Value,
            payload.ReaderUserId,
            payload.ConversationId,
            payload.ReadAt,
            cancellationToken);
    }

    /// <summary>
    /// Queues the "still unread?" check for a direct message. Due at the message's own
    /// <c>OccurredAt</c> plus the grace period — not at the time this handler runs — so a consumer
    /// catching up on a backlog does not silently extend everyone's grace period.
    /// </summary>
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
                envelope.OrganizationId!.Value,
                createRequest.RecipientUserId,
                createRequest.Body,
                createRequest.ActionUrl,
                conversationId,
                messageSentAt,
                dueAt),
            cancellationToken);
    }
}
