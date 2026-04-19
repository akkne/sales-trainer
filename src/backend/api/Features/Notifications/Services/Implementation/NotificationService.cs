using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.Notifications.Models;
using SalesTrainer.Api.Features.Notifications.Services.Abstract;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Notifications.Services.Implementation;

internal sealed class NotificationService(AppDbContext databaseContext) : INotificationService
{
    public async Task CreateAsync(
        Guid recipientUserId,
        NotificationType notificationType,
        string title,
        string body,
        string? actionUrl = null,
        string? relatedEntityId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            RecipientUserId = recipientUserId,
            NotificationType = notificationType,
            Title = title,
            Body = body,
            ActionUrl = actionUrl,
            RelatedEntityId = relatedEntityId,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        databaseContext.Notifications.Add(notification);
        await databaseContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationDto>> GetRecentAsync(
        Guid recipientUserId,
        int limit,
        bool includeRead,
        CancellationToken cancellationToken = default)
    {
        var query = databaseContext.Notifications
            .Where(notification => notification.RecipientUserId == recipientUserId);

        if (!includeRead)
            query = query.Where(notification => !notification.IsRead);

        var notifications = await query
            .OrderByDescending(notification => notification.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return notifications
            .Select(MapToDto)
            .ToList();
    }

    public async Task<int> GetUnreadCountAsync(
        Guid recipientUserId,
        CancellationToken cancellationToken = default)
    {
        return await databaseContext.Notifications
            .CountAsync(
                notification => notification.RecipientUserId == recipientUserId && !notification.IsRead,
                cancellationToken);
    }

    public async Task MarkAsReadAsync(
        Guid recipientUserId,
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        var notification = await databaseContext.Notifications
            .FirstOrDefaultAsync(
                existing => existing.Id == notificationId && existing.RecipientUserId == recipientUserId,
                cancellationToken)
            ?? throw new KeyNotFoundException("Notification not found.");

        if (notification.IsRead) return;

        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;
        await databaseContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAllAsReadAsync(
        Guid recipientUserId,
        CancellationToken cancellationToken = default)
    {
        var readTimestamp = DateTime.UtcNow;

        var unreadNotifications = await databaseContext.Notifications
            .Where(notification => notification.RecipientUserId == recipientUserId && !notification.IsRead)
            .ToListAsync(cancellationToken);

        if (unreadNotifications.Count == 0) return;

        foreach (var notification in unreadNotifications)
        {
            notification.IsRead = true;
            notification.ReadAt = readTimestamp;
        }

        await databaseContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> DeleteReadNotificationsOlderThanAsync(
        DateTime thresholdUtc,
        CancellationToken cancellationToken = default)
    {
        return await databaseContext.Notifications
            .Where(notification => notification.IsRead && notification.CreatedAt < thresholdUtc)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static NotificationDto MapToDto(Notification notification) =>
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
