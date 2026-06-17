namespace SalesTrainer.Api.Features.Metrics.Services.Abstract;

/// <summary>
/// Tracks recently-active users in Redis so an "online users" gauge can be derived.
/// Backed by a single sorted set (member = userId, score = last-seen unix seconds).
/// </summary>
public interface IPresenceTracker
{
    /// <summary>Record that <paramref name="userId"/> was just seen (now).</summary>
    Task MarkSeenAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Count users seen within the presence window (last few minutes).</summary>
    Task<long> CountOnlineAsync(CancellationToken cancellationToken = default);

    /// <summary>Drop entries older than the presence window. Called periodically.</summary>
    Task PruneAsync(CancellationToken cancellationToken = default);
}
