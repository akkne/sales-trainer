using System.Text.Json;
using Microsoft.Extensions.Options;
using Sellevate.Notification.Common.Constants;
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
///
/// <para>
/// Phase 40.13. The read watermark is per-organization state and gets the <c>org:{orgId}:</c>
/// prefix; the pending queue deliberately does <b>not</b> — see
/// <see cref="RedisKeys.ChatEmailPendingQueue"/>. The organization travels inside each queued item
/// instead, exactly as it travels in a Kafka envelope, and the dispatcher sets it on the tenant
/// context before doing anything with the item.
/// </para>
///
/// <para>
/// Items queued before 40.13 carry no organization. They are dropped on claim rather than sent
/// under a guessed tenant: the queue holds at most one grace period's worth of messages (minutes),
/// so the blast radius is a handful of "you have an unread message" emails that are not sent, and
/// the chat message itself is untouched. Recorded in docs/DONT_FORGET.md.
/// </para>
/// </summary>
public sealed class RedisDelayedChatEmailScheduler : IDelayedChatEmailScheduler
{
    /// <summary>
    /// Ranges the due members and removes them atomically, returning the claimed payloads, so two
    /// dispatcher instances can never email the same message twice.
    /// KEYS[1] pending queue; ARGV[1] cut-off score in epoch ms; ARGV[2] maximum items.
    /// </summary>
    private const string ClaimDueScript = """
        local due = redis.call('ZRANGEBYSCORE', KEYS[1], '-inf', ARGV[1], 'LIMIT', 0, tonumber(ARGV[2]))
        if #due > 0 then
            redis.call('ZREM', KEYS[1], unpack(due))
        end
        return due
        """;

    /// <summary>
    /// Raises the watermark only when the incoming read time is later than the stored one, so an
    /// out-of-order read receipt can never lower it and re-arm an email for a message already read.
    /// KEYS[1] watermark key; ARGV[1] read time in ticks; ARGV[2] TTL in seconds.
    /// </summary>
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
    private readonly ILogger<RedisDelayedChatEmailScheduler> _logger;

    public RedisDelayedChatEmailScheduler(
        IConnectionMultiplexer connection,
        IOptions<NotificationEmailConfiguration> configuration,
        ILogger<RedisDelayedChatEmailScheduler> logger)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);
        _connection = connection;
        _configuration = configuration.Value;
        _logger = logger;
    }

    private TimeSpan BookkeepingTtl => TimeSpan.FromHours(Math.Max(1, _configuration.BookkeepingRetentionHours));

    public Task ScheduleAsync(PendingChatEmail pending, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pending);

        RequireOrganization(pending.OrganizationId);

        var member = new StoredPendingChatEmail(
            Guid.NewGuid(),
            pending.OrganizationId,
            pending.RecipientUserId,
            pending.Body,
            pending.ActionUrl,
            pending.ConversationId,
            pending.MessageSentAt.Ticks);

        var payload = JsonSerializer.Serialize(member);
        var score = ToUnixMilliseconds(pending.DueAt);
        return _connection.GetDatabase().SortedSetAddAsync(RedisKeys.ChatEmailPendingQueue, payload, score);
    }

    public Task MarkConversationReadAsync(
        Guid organizationId,
        Guid readerUserId,
        Guid? conversationId,
        DateTime readAt,
        CancellationToken cancellationToken = default)
    {
        RequireOrganization(organizationId);

        return _connection.GetDatabase().ScriptEvaluateAsync(
            SetWatermarkScript,
            [WatermarkKey(organizationId, readerUserId, conversationId)],
            [readAt.Ticks, (int)BookkeepingTtl.TotalSeconds]);
    }

    public async Task<IReadOnlyList<PendingChatEmail>> ClaimDueAsync(
        DateTime asOf, int maximumItems, CancellationToken cancellationToken = default)
    {
        var result = await _connection.GetDatabase().ScriptEvaluateAsync(
            ClaimDueScript,
            [(RedisKey)RedisKeys.ChatEmailPendingQueue],
            [ToUnixMilliseconds(asOf), Math.Max(1, maximumItems)]);

        if (result.IsNull)
        {
            return [];
        }

        var members = (RedisValue[])result!;
        var claimed = new List<PendingChatEmail>(members.Length);
        foreach (var member in members)
        {
            var stored = Deserialize(member);
            if (stored is null)
            {
                continue;
            }

            if (stored.OrganizationId == Guid.Empty)
            {
                _logger.LogWarning(
                    "Dropping a pending unread-chat email with no organization (queued before 40.13)");
                continue;
            }

            claimed.Add(new PendingChatEmail(
                stored.OrganizationId,
                stored.RecipientUserId,
                stored.Body,
                stored.ActionUrl,
                stored.ConversationId,
                new DateTime(stored.MessageSentAtTicks, DateTimeKind.Utc),
                asOf));
        }

        return claimed;
    }

    public async Task<bool> WasReadAsync(PendingChatEmail pending, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pending);

        RequireOrganization(pending.OrganizationId);

        var watermark = await _connection.GetDatabase()
            .StringGetAsync(WatermarkKey(pending.OrganizationId, pending.RecipientUserId, pending.ConversationId));

        return watermark.HasValue
            && long.TryParse(watermark, out var readTicks)
            && readTicks >= pending.MessageSentAt.Ticks;
    }

    /// <summary>
    /// Returns null rather than raising on an unreadable member: the payload shape has changed once
    /// already, and one undeserializable item must not stop the whole batch behind it from being
    /// emailed. The item has already been removed from the queue by the claim, so it is simply lost —
    /// acceptable for a "you have an unread message" nudge, and never for the message itself.
    /// </summary>
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

    private static RedisKey WatermarkKey(Guid organizationId, Guid recipientUserId, Guid? conversationId) =>
        RedisKeys.ChatEmailReadWatermark(organizationId, recipientUserId, conversationId);

    /// <summary>
    /// Fails closed with the wording every other tenant guard in the codebase uses. An unset tenant
    /// on the schedule path would queue an item the dispatcher must then drop; on the read path it
    /// would look up a watermark that can never match, and every unread-chat email would be sent
    /// even to people who had already read the message.
    /// </summary>
    private static void RequireOrganization(Guid organizationId)
    {
        if (organizationId == Guid.Empty)
        {
            throw new InvalidOperationException(ErrorMessages.OrganizationContextNotSet);
        }
    }

    private static long ToUnixMilliseconds(DateTime value) =>
        new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

    /// <summary>On-the-wire shape of a queued pending email (the sorted-set member).</summary>
    private sealed record StoredPendingChatEmail(
        Guid Id,
        Guid OrganizationId,
        Guid RecipientUserId,
        string Body,
        string? ActionUrl,
        Guid? ConversationId,
        long MessageSentAtTicks);
}
