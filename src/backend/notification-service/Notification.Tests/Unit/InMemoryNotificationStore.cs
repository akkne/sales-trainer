using Sellevate.Notification.Features.Notifications.Models;
using Sellevate.Notification.Features.Notifications.Services.Abstract;

namespace Sellevate.Notification.Tests.Unit;

internal sealed class InMemoryNotificationStore : INotificationStore
{
    private readonly Dictionary<Guid, List<NotificationRecord>> _inboxesByRecipient = new();

    public TimeSpan? LastRetention { get; private set; }

    public int CapacityFor(Guid recipientUserId) =>
        _inboxesByRecipient.TryGetValue(recipientUserId, out var inbox) ? inbox.Count : 0;

    public Task PrependAsync(
        Guid recipientUserId,
        NotificationRecord notification,
        int inboxCapacity,
        TimeSpan retention,
        CancellationToken cancellationToken = default)
    {
        LastRetention = retention;

        if (!_inboxesByRecipient.TryGetValue(recipientUserId, out var inbox))
        {
            inbox = [];
            _inboxesByRecipient[recipientUserId] = inbox;
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
        Guid recipientUserId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<NotificationRecord> result = _inboxesByRecipient.TryGetValue(recipientUserId, out var inbox)
            ? inbox.ToList()
            : [];
        return Task.FromResult(result);
    }

    public Task<bool> ReplaceAsync(
        Guid recipientUserId,
        NotificationRecord updatedNotification,
        TimeSpan retention,
        CancellationToken cancellationToken = default)
    {
        LastRetention = retention;

        if (!_inboxesByRecipient.TryGetValue(recipientUserId, out var inbox))
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
        Guid recipientUserId,
        IReadOnlyList<NotificationRecord> notifications,
        TimeSpan retention,
        CancellationToken cancellationToken = default)
    {
        LastRetention = retention;

        if (!_inboxesByRecipient.TryGetValue(recipientUserId, out var inbox))
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
