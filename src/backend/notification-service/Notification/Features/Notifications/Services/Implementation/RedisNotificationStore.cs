using System.Text.Json;
using Sellevate.Notification.Common.Constants;
using Sellevate.Notification.Features.Notifications.Models;
using Sellevate.Notification.Features.Notifications.Services.Abstract;
using StackExchange.Redis;

namespace Sellevate.Notification.Features.Notifications.Services.Implementation;

internal sealed class RedisNotificationStore : INotificationStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    // Atomically prepend a new item, trim the list to capacity, and refresh the TTL.
    // KEYS[1] = inbox key
    // ARGV[1] = serialized notification
    // ARGV[2] = max capacity (minimum 1)
    // ARGV[3] = TTL in seconds
    private const string PrependLua = @"
redis.call('LPUSH', KEYS[1], ARGV[1])
redis.call('LTRIM', KEYS[1], 0, math.max(tonumber(ARGV[2]) - 1, 0))
redis.call('EXPIRE', KEYS[1], ARGV[3])
return 1
";

    // Atomically replace a single list element identified by its JSON id field, then refresh TTL.
    // KEYS[1] = inbox key
    // ARGV[1] = notification id (string)
    // ARGV[2] = replacement JSON
    // ARGV[3] = TTL in seconds
    // Returns 1 if replaced, 0 if not found.
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

    // Atomically replace multiple list elements by id, then refresh TTL.
    // KEYS[1] = inbox key
    // ARGV[1] = TTL in seconds
    // ARGV[2..N] = replacement JSON items (each has an 'id' field)
    // Returns number of replacements made.
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
        Guid recipientUserId,
        NotificationRecord notification,
        int inboxCapacity,
        TimeSpan retention,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var database = _connectionMultiplexer.GetDatabase();
        var inboxKey = RedisKeys.Inbox(recipientUserId);

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
        Guid recipientUserId,
        IReadOnlyList<NotificationRecord> notifications,
        TimeSpan retention,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notifications);

        if (notifications.Count == 0)
            return;

        var database = _connectionMultiplexer.GetDatabase();
        var inboxKey = RedisKeys.Inbox(recipientUserId);

        // ARGV[1] = TTL, ARGV[2..N] = serialized replacement items
        var args = new RedisValue[1 + notifications.Count];
        args[0] = (long)retention.TotalSeconds;
        for (var i = 0; i < notifications.Count; i++)
            args[i + 1] = Serialize(notifications[i]);

        await database.ScriptEvaluateAsync(
            ReplaceAllLua,
            keys: [(RedisKey)inboxKey],
            values: args);
    }

    private static RedisValue Serialize(NotificationRecord notification) =>
        JsonSerializer.Serialize(notification, SerializerOptions);

    private static NotificationRecord? Deserialize(RedisValue entry) =>
        entry.IsNullOrEmpty ? null : JsonSerializer.Deserialize<NotificationRecord>(entry!, SerializerOptions);
}
