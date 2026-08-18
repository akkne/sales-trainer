using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Notification.Common;
using Sellevate.Notification.Common.Constants;
using Sellevate.Notification.Features.Notifications.Emails;
using Sellevate.Notification.Features.Notifications.Models;
using Sellevate.Notification.Features.Notifications.Services.Abstract;
using Sellevate.Notification.Infrastructure.Configuration;

namespace Sellevate.Notification.Features.Notifications.Services.Implementation;

/// <summary>
/// Phase 40.13. The single place notification-service turns an ambient tenant into an explicit one.
/// Every store call below passes <see cref="CurrentOrganizationId"/>, so the organization reaches
/// the Redis key through the type system rather than through a convention somebody has to remember.
/// </summary>
internal sealed class NotificationService : INotificationService
{
    private readonly INotificationStore _notificationStore;
    private readonly INotificationEmailDispatcher _emailDispatcher;
    private readonly ITenantContext _tenantContext;
    private readonly NotificationStorageConfiguration _storageConfiguration;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        INotificationStore notificationStore,
        INotificationEmailDispatcher emailDispatcher,
        ITenantContext tenantContext,
        IOptions<NotificationStorageConfiguration> storageConfiguration,
        ILogger<NotificationService> logger)
    {
        ArgumentNullException.ThrowIfNull(notificationStore);
        ArgumentNullException.ThrowIfNull(emailDispatcher);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(storageConfiguration);
        ArgumentNullException.ThrowIfNull(logger);

        _notificationStore = notificationStore;
        _emailDispatcher = emailDispatcher;
        _tenantContext = tenantContext;
        _storageConfiguration = storageConfiguration.Value;
        _logger = logger;
    }

    /// <summary>
    /// Raises when the tenant is unset. An inbox has no platform-global reading: system mode here
    /// would mean "every organization's notifications", which is the failure 40.14 is written to
    /// catch, so this service has no system-mode path at all.
    /// </summary>
    private Guid CurrentOrganizationId =>
        _tenantContext.OrganizationId ?? throw new InvalidOperationException(ErrorMessages.OrganizationContextNotSet);

    /// <summary>
    /// Stores the notification and, when the request opts in, mails it.
    ///
    /// <para>
    /// <b>Domain-level idempotency.</b> A notification whose (recipient, type, relatedEntityId)
    /// is already in the inbox is skipped, which is what makes a Kafka redelivery harmless. It
    /// applies only where <c>relatedEntityId</c> uniquely identifies the originating domain fact —
    /// see <see cref="IsDeduplicatable"/> — and a skipped notification is also never emailed.
    /// </para>
    ///
    /// <para>
    /// <b>Untrusted input.</b> Title and body arrive from other services' events and are stripped of
    /// control characters before storage; the action url is narrowed to a relative app path. Stored
    /// values are plain text and the frontend MUST render them as text, never as inner HTML.
    /// </para>
    /// </summary>
    public async Task CreateAsync(CreateNotificationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Body);

        var organizationId = CurrentOrganizationId;

        if (IsDeduplicatable(request) && await _notificationStore.ExistsAsync(
                organizationId,
                request.RecipientUserId,
                request.NotificationType,
                request.RelatedEntityId,
                cancellationToken))
        {
            _logger.LogDebug(
                "Skipping duplicate {NotificationType} notification for recipient {RecipientUserId} (relatedEntityId={RelatedEntityId})",
                request.NotificationType, request.RecipientUserId, request.RelatedEntityId);
            return;
        }

        var sanitizedTitle = InputSanitizer.StripControlCharacters(request.Title);
        var sanitizedBody = InputSanitizer.StripControlCharacters(request.Body);
        var sanitizedActionUrl = InputSanitizer.SanitizeActionUrl(request.ActionUrl);

        var notification = new NotificationRecord
        {
            Id = Guid.NewGuid(),
            RecipientUserId = request.RecipientUserId,
            NotificationType = request.NotificationType,
            Title = sanitizedTitle,
            Body = sanitizedBody,
            ActionUrl = sanitizedActionUrl,
            RelatedEntityId = request.RelatedEntityId,
            IsRead = false,
            CreatedAt = DateTime.UtcNow,
            ReadAt = null
        };

        await _notificationStore.PrependAsync(
            organizationId,
            request.RecipientUserId,
            notification,
            _storageConfiguration.InboxCapacity,
            Retention,
            cancellationToken);

        _logger.LogInformation(
            "Stored {NotificationType} notification for recipient {RecipientUserId}",
            request.NotificationType, request.RecipientUserId);

        if (request.SendEmail)
        {
            await _emailDispatcher.DispatchAsync(
                request.RecipientUserId,
                request.NotificationType,
                sanitizedTitle,
                sanitizedBody,
                sanitizedActionUrl,
                cancellationToken);
        }
    }

    /// <summary>
    /// True only when <c>relatedEntityId</c> uniquely identifies the originating domain fact — a
    /// friendship, an achievement key, a streak day, a note. Chat messages are excluded on purpose:
    /// they all carry the conversation id, so every message is genuinely a new notification and
    /// deduplicating them would silence a whole conversation after its first message. A request with
    /// no <c>relatedEntityId</c> is likewise never deduplicated, because there is nothing to compare.
    /// </summary>
    private static bool IsDeduplicatable(CreateNotificationRequest request) =>
        !string.IsNullOrWhiteSpace(request.RelatedEntityId)
        && request.NotificationType != NotificationType.ChatMessageReceived;

    public async Task<IReadOnlyList<NotificationDto>> GetRecentAsync(
        Guid recipientUserId,
        int limit,
        bool includeRead,
        CancellationToken cancellationToken = default)
    {
        var notifications = await _notificationStore.GetAllAsync(
            CurrentOrganizationId, recipientUserId, cancellationToken);

        return notifications
            .Where(notification => includeRead || !notification.IsRead)
            .OrderByDescending(notification => notification.CreatedAt)
            .Take(limit)
            .Select(MapToDto)
            .ToList();
    }

    public async Task<int> GetUnreadCountAsync(Guid recipientUserId, CancellationToken cancellationToken = default)
    {
        var notifications = await _notificationStore.GetAllAsync(
            CurrentOrganizationId, recipientUserId, cancellationToken);
        return notifications.Count(notification => !notification.IsRead);
    }

    public async Task MarkAsReadAsync(
        Guid recipientUserId,
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        var organizationId = CurrentOrganizationId;
        var notifications = await _notificationStore.GetAllAsync(organizationId, recipientUserId, cancellationToken);

        var target = notifications.FirstOrDefault(notification => notification.Id == notificationId)
            ?? throw new KeyNotFoundException(ErrorMessages.NotificationNotFound);

        if (target.IsRead)
        {
            return;
        }

        var updated = target with { IsRead = true, ReadAt = DateTime.UtcNow };
        await _notificationStore.ReplaceAsync(organizationId, recipientUserId, updated, Retention, cancellationToken);
    }

    public async Task MarkAllAsReadAsync(Guid recipientUserId, CancellationToken cancellationToken = default)
    {
        var organizationId = CurrentOrganizationId;
        var notifications = await _notificationStore.GetAllAsync(organizationId, recipientUserId, cancellationToken);

        if (notifications.Count == 0 || notifications.All(notification => notification.IsRead))
        {
            return;
        }

        var readTimestamp = DateTime.UtcNow;
        var updated = notifications
            .Select(notification => notification.IsRead
                ? notification
                : notification with { IsRead = true, ReadAt = readTimestamp })
            .ToList();

        await _notificationStore.ReplaceAllAsync(organizationId, recipientUserId, updated, Retention, cancellationToken);
    }

    private TimeSpan Retention => TimeSpan.FromDays(Math.Max(1, _storageConfiguration.RetentionDays));

    private static NotificationDto MapToDto(NotificationRecord notification) =>
        new(
            notification.Id,
            notification.NotificationType.ToString(),
            notification.Title,
            notification.Body,
            notification.ActionUrl,
            notification.RelatedEntityId,
            notification.IsRead,
            notification.CreatedAt,
            notification.ReadAt);
}
