using System.Text.Json;
using Sellevate.Notification.Common.Constants;
using Sellevate.Notification.Features.Notifications.Models;
using Sellevate.Notification.Features.Notifications.Services.Abstract;
using StackExchange.Redis;

namespace Sellevate.Notification.Features.Notifications.Services.Implementation;

internal sealed class RedisNotificationStore : INotificationStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer _connectionMultiplexer;

    public RedisNotificationStore(IConnectionMultiplexer connectionMultiplexer)
    {
        ArgumentNullException.ThrowIfNull(connectionMultiplexer);
        _connectionMultiplexer = connectionMultiplexer;
    }

    public async Task PrependAsync(
        Guid recipientUserId,
        NotificationRecord notification,
        int inboxCapacity,
        TimeSpan retention,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var database = _connectionMultiplexer.GetDatabase();
        var inboxKey = RedisKeys.Inbox(recipientUserId);

        await database.ListLeftPushAsync(inboxKey, Serialize(notification));
        await database.ListTrimAsync(inboxKey, 0, Math.Max(1, inboxCapacity) - 1);
        await database.KeyExpireAsync(inboxKey, retention);

        await RefreshUnreadCounterAsync(database, recipientUserId, inboxKey, retention);
    }

    public async Task<IReadOnlyList<NotificationRecord>> GetAllAsync(
        Guid recipientUserId,
        CancellationToken cancellationToken = default)
    {
        var database = _connectionMultiplexer.GetDatabase();
        var entries = await database.ListRangeAsync(RedisKeys.Inbox(recipientUserId));

        return entries
            .Select(entry => Deserialize(entry))
            .Where(record => record is not null)
            .Select(record => record!)
            .ToList();
    }

    public async Task<bool> ReplaceAsync(
        Guid recipientUserId,
        NotificationRecord updatedNotification,
        TimeSpan retention,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(updatedNotification);

        var database = _connectionMultiplexer.GetDatabase();
        var inboxKey = RedisKeys.Inbox(recipientUserId);
        var entries = await database.ListRangeAsync(inboxKey);

        for (var position = 0; position < entries.Length; position++)
        {
            var record = Deserialize(entries[position]);
            if (record is null || record.Id != updatedNotification.Id)
            {
                continue;
            }

            await database.ListSetByIndexAsync(inboxKey, position, Serialize(updatedNotification));
            await database.KeyExpireAsync(inboxKey, retention);
            await RefreshUnreadCounterAsync(database, recipientUserId, inboxKey, retention);
            return true;
        }

        return false;
    }

    public async Task ReplaceAllAsync(
        Guid recipientUserId,
        IReadOnlyList<NotificationRecord> notifications,
        TimeSpan retention,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notifications);

        var database = _connectionMultiplexer.GetDatabase();
        var inboxKey = RedisKeys.Inbox(recipientUserId);
        var entries = await database.ListRangeAsync(inboxKey);

        for (var position = 0; position < entries.Length; position++)
        {
            var existing = Deserialize(entries[position]);
            if (existing is null)
            {
                continue;
            }

            var replacement = notifications.FirstOrDefault(notification => notification.Id == existing.Id);
            if (replacement is not null)
            {
                await database.ListSetByIndexAsync(inboxKey, position, Serialize(replacement));
            }
        }

        await database.KeyExpireAsync(inboxKey, retention);
        await RefreshUnreadCounterAsync(database, recipientUserId, inboxKey, retention);
    }

    private static async Task RefreshUnreadCounterAsync(
        IDatabase database,
        Guid recipientUserId,
        RedisKey inboxKey,
        TimeSpan retention)
    {
        var entries = await database.ListRangeAsync(inboxKey);
        var unreadCount = entries
            .Select(entry => Deserialize(entry))
            .Count(record => record is { IsRead: false });

        var unreadKey = RedisKeys.UnreadCount(recipientUserId);
        await database.StringSetAsync(unreadKey, unreadCount, retention);
    }

    private static RedisValue Serialize(NotificationRecord notification) =>
        JsonSerializer.Serialize(notification, SerializerOptions);

    private static NotificationRecord? Deserialize(RedisValue entry) =>
        entry.IsNullOrEmpty ? null : JsonSerializer.Deserialize<NotificationRecord>(entry!, SerializerOptions);
}
