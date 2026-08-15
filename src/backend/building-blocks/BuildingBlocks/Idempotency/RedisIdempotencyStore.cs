using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Messaging;
using StackExchange.Redis;

namespace Sellevate.BuildingBlocks.Idempotency;

/// <summary>
/// Redis-backed idempotency store. Each processed event is a key
/// <c>org:{organizationId}:idem:{group}:{eventId}</c> with a TTL, so the dedupe set self-prunes —
/// the TTL is long enough to outlast any realistic broker redelivery window. A
/// per-partition consumer processes sequentially, so the non-atomic
/// check-then-mark pair carries no practical race.
///
/// <para>
/// Phase 40.11: the organization leads the key, so no two tenants share a dedupe namespace and a
/// Redis key listing tells an operator which customer a key belongs to. An event with no
/// organization (platform-global) keeps the historical <c>idem:{group}:{eventId}</c> shape —
/// there is no tenant to mix up, and keeping it means events processed before this change are
/// still recognized. Events that DO carry an organization change key shape once, so the first
/// redelivery of an already-handled org event after deployment can be processed a second time;
/// every handler in this codebase is idempotent by construction, and the window is one TTL wide.
/// See docs/DECISIONS.md (2026-08-15, "old Redis keys expire, they are not flushed").
/// </para>
/// </summary>
public sealed class RedisIdempotencyStore : IIdempotencyStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly TimeSpan _ttl;

    public RedisIdempotencyStore(IConnectionMultiplexer redis, IOptions<KafkaSettings> settings)
    {
        _redis = redis;
        _ttl = TimeSpan.FromDays(Math.Max(1, settings.Value.IdempotencyTtlDays));
    }

    public async Task<bool> HasProcessedAsync(
        string consumerGroup,
        Guid eventId,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default)
        => await _redis.GetDatabase().KeyExistsAsync(Key(consumerGroup, eventId, organizationId));

    public async Task MarkProcessedAsync(
        string consumerGroup,
        Guid eventId,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default)
        => await _redis.GetDatabase().StringSetAsync(Key(consumerGroup, eventId, organizationId), "1", _ttl);

    internal static string Key(string consumerGroup, Guid eventId, Guid? organizationId)
        => organizationId is { } tenant
            ? $"org:{tenant}:idem:{consumerGroup}:{eventId:N}"
            : $"idem:{consumerGroup}:{eventId:N}";
}
