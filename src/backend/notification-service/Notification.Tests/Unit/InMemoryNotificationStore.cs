using Sellevate.Notification.Features.Notifications.Models;
using Sellevate.Notification.Features.Notifications.Services.Abstract;

namespace Sellevate.Notification.Tests.Unit;

/// <summary>
/// Phase 40.13 keyed this fake by (organization, recipient) rather than by recipient alone, so it
/// models the real Redis key instead of a flattened version of it. A fake that ignores the
/// organization would make a cross-tenant isolation test pass for the wrong reason.
/// </summary>
internal sealed class InMemoryNotificationStore : INotificationStore
{
    private readonly Dictionary<(Guid OrganizationId, Guid RecipientUserId), List<NotificationRecord>> _inboxes = new();

    public TimeSpan? LastRetention { get; private set; }

    public int CapacityFor(Guid organizationId, Guid recipientUserId) =>
        _inboxes.TryGetValue((organizationId, recipientUserId), out var inbox) ? inbox.Count : 0;

    public Task PrependAsync(
        Guid organizationId,
        Guid recipientUserId,
        NotificationRecord notification,
        int inboxCapacity,
        TimeSpan retention,
        CancellationToken cancellationToken = default)
    {
        LastRetention = retention;

        if (!_inboxes.TryGetValue((organizationId, recipientUserId), out var inbox))
        {
            inbox = [];
            _inboxes[(organizationId, recipientUserId)] = inbox;
        }

        inbox.Insert(0, notification);

        var capacity = Math.Max(1, inboxCapacity);
        if (inbox.Count > capacity)
        {
            inbox.RemoveRange(capacity, inbox.Count - capacity);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<NotificationRecord>> GetAllAsync(
        Guid organizationId,
        Guid recipientUserId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<NotificationRecord> result = _inboxes.TryGetValue((organizationId, recipientUserId), out var inbox)
            ? inbox.ToList()
            : [];
        return Task.FromResult(result);
    }

    public Task<bool> ExistsAsync(
        Guid organizationId,
        Guid recipientUserId,
        NotificationType notificationType,
        string? relatedEntityId,
        CancellationToken cancellationToken = default)
    {
        if (!_inboxes.TryGetValue((organizationId, recipientUserId), out var inbox))
            return Task.FromResult(false);

        return Task.FromResult(inbox.Any(n =>
            n.NotificationType == notificationType &&
            n.RelatedEntityId == relatedEntityId));
    }

    public Task<bool> ReplaceAsync(
        Guid organizationId,
        Guid recipientUserId,
        NotificationRecord updatedNotification,
        TimeSpan retention,
        CancellationToken cancellationToken = default)
    {
        LastRetention = retention;

        if (!_inboxes.TryGetValue((organizationId, recipientUserId), out var inbox))
        {
            return Task.FromResult(false);
        }

        var position = inbox.FindIndex(record => record.Id == updatedNotification.Id);
        if (position < 0)
        {
            return Task.FromResult(false);
        }

        inbox[position] = updatedNotification;
        return Task.FromResult(true);
    }

    public Task ReplaceAllAsync(
        Guid organizationId,
        Guid recipientUserId,
        IReadOnlyList<NotificationRecord> notifications,
        TimeSpan retention,
        CancellationToken cancellationToken = default)
    {
        LastRetention = retention;

        if (!_inboxes.TryGetValue((organizationId, recipientUserId), out var inbox))
        {
            return Task.CompletedTask;
        }

        for (var position = 0; position < inbox.Count; position++)
        {
            var replacement = notifications.FirstOrDefault(notification => notification.Id == inbox[position].Id);
            if (replacement is not null)
            {
                inbox[position] = replacement;
            }
        }

        return Task.CompletedTask;
    }
}
