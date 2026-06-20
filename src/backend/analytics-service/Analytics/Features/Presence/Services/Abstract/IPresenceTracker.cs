namespace Sellevate.Analytics.Features.Presence.Services.Abstract;

public interface IPresenceTracker
{
    Task MarkSeenAsync(string userId, CancellationToken cancellationToken = default);

    Task<long> CountOnlineAsync(CancellationToken cancellationToken = default);

    Task PruneAsync(CancellationToken cancellationToken = default);
}
