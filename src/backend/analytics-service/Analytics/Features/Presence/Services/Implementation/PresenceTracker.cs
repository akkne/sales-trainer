using Sellevate.Analytics.Features.Presence.Services.Abstract;
using StackExchange.Redis;

namespace Sellevate.Analytics.Features.Presence.Services.Implementation;

internal sealed class PresenceTracker : IPresenceTracker
{
    private const string OnlineKey = "presence:online";
    private static readonly TimeSpan PresenceWindow = TimeSpan.FromMinutes(5);

    private readonly IConnectionMultiplexer _redisConnection;

    public PresenceTracker(IConnectionMultiplexer redisConnection)
    {
        ArgumentNullException.ThrowIfNull(redisConnection);
        _redisConnection = redisConnection;
    }

    public Task MarkSeenAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var lastSeenUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return _redisConnection.GetDatabase().SortedSetAddAsync(OnlineKey, userId, lastSeenUnixSeconds);
    }

    public async Task<long> CountOnlineAsync(CancellationToken cancellationToken = default)
    {
        var windowStartUnixSeconds = DateTimeOffset.UtcNow.Subtract(PresenceWindow).ToUnixTimeSeconds();
        return await _redisConnection.GetDatabase()
            .SortedSetLengthAsync(OnlineKey, windowStartUnixSeconds, double.PositiveInfinity);
    }

    public Task PruneAsync(CancellationToken cancellationToken = default)
    {
        var windowStartUnixSeconds = DateTimeOffset.UtcNow.Subtract(PresenceWindow).ToUnixTimeSeconds();
        return _redisConnection.GetDatabase()
            .SortedSetRemoveRangeByScoreAsync(OnlineKey, double.NegativeInfinity, windowStartUnixSeconds, Exclude.Stop);
    }
}
