using SalesTrainer.Api.Features.Metrics.Services.Abstract;
using StackExchange.Redis;

namespace SalesTrainer.Api.Features.Metrics.Services;

/// <summary>
/// Redis-backed presence tracking. A single sorted set <c>presence:online</c> holds one
/// member per user with the last-seen unix-second timestamp as the score. Counting and
/// pruning are O(log N) range operations on that one key — no SCAN, no per-user keys.
/// </summary>
public sealed class PresenceTracker : IPresenceTracker
{
    private const string OnlineKey = "presence:online";
    private static readonly TimeSpan PresenceWindow = TimeSpan.FromMinutes(5);

    private readonly IConnectionMultiplexer _redis;

    public PresenceTracker(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public Task MarkSeenAsync(string userId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return _redis.GetDatabase().SortedSetAddAsync(OnlineKey, userId, now);
    }

    public async Task<long> CountOnlineAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow.Subtract(PresenceWindow).ToUnixTimeSeconds();
        return await _redis.GetDatabase()
            .SortedSetLengthAsync(OnlineKey, cutoff, double.PositiveInfinity);
    }

    public Task PruneAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow.Subtract(PresenceWindow).ToUnixTimeSeconds();
        return _redis.GetDatabase()
            .SortedSetRemoveRangeByScoreAsync(OnlineKey, double.NegativeInfinity, cutoff, Exclude.Stop);
    }
}
