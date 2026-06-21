using System.Text.Json;
using Microsoft.Extensions.Options;
using Sellevate.Notification.Infrastructure.Configuration;
using StackExchange.Redis;

namespace Sellevate.Notification.Features.Notifications.Emails.Delayed;

/// <summary>
/// Redis-backed <see cref="IDelayedChatEmailScheduler"/>.
/// <list type="bullet">
/// <item>Pending emails live in a sorted set scored by their due time (epoch ms); the payload is
/// the set member (JSON with a nonce so identical messages never collapse).</item>
/// <item>Read receipts are a per-(recipient, conversation) watermark holding the latest read time,
/// updated monotonically so an out-of-order receipt can't lower it.</item>
/// </list>
/// Claiming is a single Lua script (range + remove) so multiple dispatcher instances never email
/// the same message twice.
/// </summary>
public sealed class RedisDelayedChatEmailScheduler : IDelayedChatEmailScheduler
{
    private const string PendingKey = "notifications:chat-email:pending";

    // Range due members and remove them atomically; returns the claimed member payloads.
    private const string ClaimDueScript = """
        local due = redis.call('ZRANGEBYSCORE', KEYS[1], '-inf', ARGV[1], 'LIMIT', 0, tonumber(ARGV[2]))
        if #due > 0 then
            redis.call('ZREM', KEYS[1], unpack(due))
        end
        return due
        """;

    // Set the watermark only when the new read time is later than the stored one.
    private const string SetWatermarkScript = """
        local current = tonumber(redis.call('GET', KEYS[1]))
        local incoming = tonumber(ARGV[1])
        if current == nil or incoming > current then
            redis.call('SET', KEYS[1], ARGV[1], 'EX', tonumber(ARGV[2]))
        end
        return 1
        """;

    private readonly IConnectionMultiplexer _connection;
    private readonly NotificationEmailConfiguration _configuration;

    public RedisDelayedChatEmailScheduler(
        IConnectionMultiplexer connection,
        IOptions<NotificationEmailConfiguration> configuration)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(configuration);
        _connection = connection;
        _configuration = configuration.Value;
    }

    private TimeSpan BookkeepingTtl => TimeSpan.FromHours(Math.Max(1, _configuration.BookkeepingRetentionHours));

    public Task ScheduleAsync(PendingChatEmail pending, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pending);

        var member = new StoredPendingChatEmail(
            Guid.NewGuid(),
            pending.RecipientUserId,
            pending.Body,
            pending.ActionUrl,
            pending.ConversationId,
            pending.MessageSentAt.Ticks);

        var payload = JsonSerializer.Serialize(member);
        var score = ToUnixMilliseconds(pending.DueAt);
        return _connection.GetDatabase().SortedSetAddAsync(PendingKey, payload, score);
    }

    public Task MarkConversationReadAsync(
        Guid readerUserId, Guid? conversationId, DateTime readAt, CancellationToken cancellationToken = default)
    {
        return _connection.GetDatabase().ScriptEvaluateAsync(
            SetWatermarkScript,
            [WatermarkKey(readerUserId, conversationId)],
            [readAt.Ticks, (int)BookkeepingTtl.TotalSeconds]);
    }

    public async Task<IReadOnlyList<PendingChatEmail>> ClaimDueAsync(
        DateTime asOf, int maxItems, CancellationToken cancellationToken = default)
    {
        var result = await _connection.GetDatabase().ScriptEvaluateAsync(
            ClaimDueScript,
            [(RedisKey)PendingKey],
            [ToUnixMilliseconds(asOf), Math.Max(1, maxItems)]);

        if (result.IsNull)
        {
            return [];
        }

        var members = (RedisValue[])result!;
        var claimed = new List<PendingChatEmail>(members.Length);
        foreach (var member in members)
        {
            var stored = Deserialize(member);
            if (stored is not null)
            {
                claimed.Add(new PendingChatEmail(
                    stored.RecipientUserId,
                    stored.Body,
                    stored.ActionUrl,
                    stored.ConversationId,
                    new DateTime(stored.MessageSentAtTicks, DateTimeKind.Utc),
                    asOf));
            }
        }

        return claimed;
    }

    public async Task<bool> WasReadAsync(PendingChatEmail pending, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pending);

        var watermark = await _connection.GetDatabase()
            .StringGetAsync(WatermarkKey(pending.RecipientUserId, pending.ConversationId));

        return watermark.HasValue
            && long.TryParse(watermark, out var readTicks)
            && readTicks >= pending.MessageSentAt.Ticks;
    }

    private static StoredPendingChatEmail? Deserialize(RedisValue member)
    {
        if (!member.HasValue)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<StoredPendingChatEmail>(member!);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static RedisKey WatermarkKey(Guid recipientUserId, Guid? conversationId) =>
        $"notifications:chat-email:read:{recipientUserId:N}:{conversationId?.ToString("N") ?? "none"}";

    private static long ToUnixMilliseconds(DateTime value) =>
        new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

    /// <summary>On-the-wire shape of a queued pending email (the sorted-set member).</summary>
    private sealed record StoredPendingChatEmail(
        Guid Id,
        Guid RecipientUserId,
        string Body,
        string? ActionUrl,
        Guid? ConversationId,
        long MessageSentAtTicks);
}
