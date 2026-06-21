using Microsoft.Extensions.Options;
using Sellevate.Notification.Common;
using Sellevate.Notification.Common.Constants;
using Sellevate.Notification.Features.Notifications.Models;
using Sellevate.Notification.Features.Notifications.Services.Abstract;
using Sellevate.Notification.Infrastructure.Configuration;

namespace Sellevate.Notification.Features.Notifications.Services.Implementation;

internal sealed class NotificationService : INotificationService
{
    private readonly INotificationStore _notificationStore;
    private readonly NotificationStorageConfiguration _storageConfiguration;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        INotificationStore notificationStore,
        IOptions<NotificationStorageConfiguration> storageConfiguration,
        ILogger<NotificationService> logger)
    {
        ArgumentNullException.ThrowIfNull(notificationStore);
        ArgumentNullException.ThrowIfNull(storageConfiguration);
        ArgumentNullException.ThrowIfNull(logger);

        _notificationStore = notificationStore;
        _storageConfiguration = storageConfiguration.Value;
        _logger = logger;
    }

    public async Task CreateAsync(CreateNotificationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Body);

        // NO3: Domain-level idempotency — skip if a notification for the same
        // recipient + type + relatedEntityId already exists. This prevents duplicate
        // notifications when a domain event is replayed (e.g. Kafka redelivery).
        if (await _notificationStore.ExistsAsync(
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

        // NO2: Strip control characters from untrusted event fields before persisting.
        // The frontend MUST render Title and Body as plain text (not innerHTML).
        var sanitizedTitle = InputSanitizer.StripControlCharacters(request.Title);
        var sanitizedBody  = InputSanitizer.StripControlCharacters(request.Body);
        // ActionUrl is validated to relative app paths only; external URLs are rejected.
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
            request.RecipientUserId,
            notification,
            _storageConfiguration.InboxCapacity,
            Retention,
            cancellationToken);

        _logger.LogInformation(
            "Stored {NotificationType} notification for recipient {RecipientUserId}",
            request.NotificationType, request.RecipientUserId);
    }

    public async Task<IReadOnlyList<NotificationDto>> GetRecentAsync(
        Guid recipientUserId,
        int limit,
        bool includeRead,
        CancellationToken cancellationToken = default)
    {
        var notifications = await _notificationStore.GetAllAsync(recipientUserId, cancellationToken);

        return notifications
            .Where(notification => includeRead || !notification.IsRead)
            .OrderByDescending(notification => notification.CreatedAt)
            .Take(limit)
            .Select(MapToDto)
            .ToList();
    }

    public async Task<int> GetUnreadCountAsync(Guid recipientUserId, CancellationToken cancellationToken = default)
    {
        var notifications = await _notificationStore.GetAllAsync(recipientUserId, cancellationToken);
        return notifications.Count(notification => !notification.IsRead);
    }

    public async Task MarkAsReadAsync(
        Guid recipientUserId,
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        var notifications = await _notificationStore.GetAllAsync(recipientUserId, cancellationToken);

        var target = notifications.FirstOrDefault(notification => notification.Id == notificationId)
            ?? throw new KeyNotFoundException(ErrorMessages.NotificationNotFound);

        if (target.IsRead)
        {
            return;
        }

        var updated = target with { IsRead = true, ReadAt = DateTime.UtcNow };
        await _notificationStore.ReplaceAsync(recipientUserId, updated, Retention, cancellationToken);
    }

    public async Task MarkAllAsReadAsync(Guid recipientUserId, CancellationToken cancellationToken = default)
    {
        var notifications = await _notificationStore.GetAllAsync(recipientUserId, cancellationToken);

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

        await _notificationStore.ReplaceAllAsync(recipientUserId, updated, Retention, cancellationToken);
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
