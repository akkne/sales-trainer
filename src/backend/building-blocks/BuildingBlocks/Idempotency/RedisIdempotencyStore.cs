using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Messaging;
using StackExchange.Redis;

namespace Sellevate.BuildingBlocks.Idempotency;

/// <summary>
/// Redis-backed idempotency store. Each processed event is a key
/// <c>idem:{group}:{eventId}</c> with a TTL, so the dedupe set self-prunes — the
/// TTL is long enough to outlast any realistic broker redelivery window. A
/// per-partition consumer processes sequentially, so the non-atomic
/// check-then-mark pair carries no practical race.
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
        CancellationToken cancellationToken = default)
        => await _redis.GetDatabase().KeyExistsAsync(Key(consumerGroup, eventId));

    public async Task MarkProcessedAsync(
        string consumerGroup,
        Guid eventId,
        CancellationToken cancellationToken = default)
        => await _redis.GetDatabase().StringSetAsync(Key(consumerGroup, eventId), "1", _ttl);

    private static string Key(string consumerGroup, Guid eventId) => $"idem:{consumerGroup}:{eventId:N}";
}
