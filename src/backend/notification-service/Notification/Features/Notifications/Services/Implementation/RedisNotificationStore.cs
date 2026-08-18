using System.Text.Json;
using Sellevate.Notification.Common.Constants;
using Sellevate.Notification.Features.Notifications.Models;
using Sellevate.Notification.Features.Notifications.Services.Abstract;
using StackExchange.Redis;

namespace Sellevate.Notification.Features.Notifications.Services.Implementation;

/// <summary>
/// Stores each recipient's inbox as one Redis list of JSON records, newest first, capped to a
/// capacity and carrying a retention TTL that is refreshed on every write — the service has no
/// database, so this list is the whole of the notification store.
///
/// <para>
/// Every mutation runs as a Lua script rather than as a read-modify-write from C#. Two dispatchers
/// and an API request can touch one inbox at the same time, and only server-side atomicity keeps a
/// concurrent "mark all as read" from resurrecting a notification the cap had just trimmed away.
/// </para>
///
/// <para>
/// A record that fails to deserialize is skipped rather than raising: an inbox holding one entry
/// written by an older shape must still be readable, and a bell that returns 500 is worse than a
/// bell missing one row.
/// </para>
/// </summary>
internal sealed class RedisNotificationStore : INotificationStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Prepends the item, trims the list to capacity and refreshes the TTL in one round trip.
    /// KEYS[1] inbox key; ARGV[1] serialized notification; ARGV[2] capacity (minimum 1);
    /// ARGV[3] TTL in seconds.
    /// </summary>
    private const string PrependLua = @"
redis.call('LPUSH', KEYS[1], ARGV[1])
redis.call('LTRIM', KEYS[1], 0, math.max(tonumber(ARGV[2]) - 1, 0))
redis.call('EXPIRE', KEYS[1], ARGV[3])
return 1
";

    /// <summary>
    /// Replaces the one element whose JSON <c>id</c> matches, then refreshes the TTL. Returns 1 when
    /// replaced and 0 when the id is no longer in the list — which is not an error: the cap may have
    /// trimmed it between the read and this write.
    /// KEYS[1] inbox key; ARGV[1] notification id; ARGV[2] replacement JSON; ARGV[3] TTL in seconds.
    /// </summary>
    private const string ReplaceOneLua = @"
local entries = redis.call('LRANGE', KEYS[1], 0, -1)
for i, entry in ipairs(entries) do
    local ok, rec = pcall(cjson.decode, entry)
    if ok and rec and tostring(rec['id']) == ARGV[1] then
        redis.call('LSET', KEYS[1], i - 1, ARGV[2])
        redis.call('EXPIRE', KEYS[1], ARGV[3])
        return 1
    end
end
return 0
";

    /// <summary>
    /// Replaces every element whose id appears among the replacements, then refreshes the TTL, and
    /// returns how many were replaced. Indexes by id rather than by position so a concurrent trim
    /// cannot make it overwrite the wrong row.
    /// KEYS[1] inbox key; ARGV[1] TTL in seconds; ARGV[2..N] replacement JSON items.
    /// </summary>
    private const string ReplaceAllLua = @"
local entries = redis.call('LRANGE', KEYS[1], 0, -1)
local replacements = {}
for i = 2, #ARGV do
    local ok, rec = pcall(cjson.decode, ARGV[i])
    if ok and rec then
        replacements[tostring(rec['id'])] = ARGV[i]
    end
end
local count = 0
for i, entry in ipairs(entries) do
    local ok, rec = pcall(cjson.decode, entry)
    if ok and rec then
        local replacement = replacements[tostring(rec['id'])]
        if replacement then
            redis.call('LSET', KEYS[1], i - 1, replacement)
            count = count + 1
        end
    end
end
redis.call('EXPIRE', KEYS[1], ARGV[1])
return count
";

    private readonly IConnectionMultiplexer _connectionMultiplexer;

    public RedisNotificationStore(IConnectionMultiplexer connectionMultiplexer)
    {
        ArgumentNullException.ThrowIfNull(connectionMultiplexer);
        _connectionMultiplexer = connectionMultiplexer;
    }

    public async Task PrependAsync(
        Guid organizationId,
        Guid recipientUserId,
        NotificationRecord notification,
        int inboxCapacity,
        TimeSpan retention,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var database = _connectionMultiplexer.GetDatabase();
        var inboxKey = RedisKeys.Inbox(organizationId, recipientUserId);

        await database.ScriptEvaluateAsync(
            PrependLua,
            keys: [(RedisKey)inboxKey],
            values:
            [
                Serialize(notification),
                Math.Max(1, inboxCapacity),
                (long)retention.TotalSeconds,
            ]);
    }

    public async Task<IReadOnlyList<NotificationRecord>> GetAllAsync(
        Guid organizationId,
        Guid recipientUserId,
        CancellationToken cancellationToken = default)
    {
        var database = _connectionMultiplexer.GetDatabase();
        var entries = await database.ListRangeAsync(RedisKeys.Inbox(organizationId, recipientUserId));

        return entries
            .Select(entry => Deserialize(entry))
            .Where(record => record is not null)
            .Select(record => record!)
            .ToList();
    }

    public async Task<bool> ExistsAsync(
        Guid organizationId,
        Guid recipientUserId,
        NotificationType notificationType,
        string? relatedEntityId,
        CancellationToken cancellationToken = default)
    {
        var notifications = await GetAllAsync(organizationId, recipientUserId, cancellationToken);
        return notifications.Any(notification =>
            notification.NotificationType == notificationType &&
            notification.RelatedEntityId == relatedEntityId);
    }

    public async Task<bool> ReplaceAsync(
        Guid organizationId,
        Guid recipientUserId,
        NotificationRecord updatedNotification,
        TimeSpan retention,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(updatedNotification);

        var database = _connectionMultiplexer.GetDatabase();
        var inboxKey = RedisKeys.Inbox(organizationId, recipientUserId);

        var result = await database.ScriptEvaluateAsync(
            ReplaceOneLua,
            keys: [(RedisKey)inboxKey],
            values:
            [
                updatedNotification.Id.ToString(),
                Serialize(updatedNotification),
                (long)retention.TotalSeconds,
            ]);

        return (long)result == 1;
    }

    public async Task ReplaceAllAsync(
        Guid organizationId,
        Guid recipientUserId,
        IReadOnlyList<NotificationRecord> notifications,
        TimeSpan retention,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notifications);

        if (notifications.Count == 0)
            return;

        var database = _connectionMultiplexer.GetDatabase();
        var inboxKey = RedisKeys.Inbox(organizationId, recipientUserId);

        var scriptArguments = new RedisValue[1 + notifications.Count];
        scriptArguments[0] = (long)retention.TotalSeconds;
        for (var index = 0; index < notifications.Count; index++)
            scriptArguments[index + 1] = Serialize(notifications[index]);

        await database.ScriptEvaluateAsync(
            ReplaceAllLua,
            keys: [(RedisKey)inboxKey],
            values: scriptArguments);
    }

    private static RedisValue Serialize(NotificationRecord notification) =>
        JsonSerializer.Serialize(notification, SerializerOptions);

    private static NotificationRecord? Deserialize(RedisValue entry) =>
        entry.IsNullOrEmpty ? null : JsonSerializer.Deserialize<NotificationRecord>(entry!, SerializerOptions);
}
