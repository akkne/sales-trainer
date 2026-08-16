using Sellevate.Analytics.Features.Presence.Services.Abstract;
using Sellevate.Analytics.Infrastructure.Metrics;

namespace Sellevate.Analytics.Features.Presence.Services.Implementation;

internal sealed class PresenceGaugeUpdaterService : BackgroundService
{
    // Gauge refresh cadence — must be faster than the Prometheus scrape interval (15 s).
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(20);

    // Prune stale Redis members on a slower cadence to avoid redundant writes on every tick.
    // Counting already filters by the window, so pruning is purely for compaction.
    private static readonly TimeSpan PruneInterval = TimeSpan.FromMinutes(5);

    // Per-process startup jitter: spread replicas across the 20 s window so they don't all
    // hit Redis simultaneously. Random.Shared is thread-safe and fine in service code.
    private static readonly TimeSpan StartupJitter =
        TimeSpan.FromMilliseconds(Random.Shared.Next(0, (int)UpdateInterval.TotalMilliseconds));

    private readonly IPresenceTracker _presenceTracker;
    private readonly ILogger<PresenceGaugeUpdaterService> _logger;
    private DateTime _lastPruneUtc = DateTime.MinValue;

    public PresenceGaugeUpdaterService(
        IPresenceTracker presenceTracker,
        ILogger<PresenceGaugeUpdaterService> logger)
    {
        ArgumentNullException.ThrowIfNull(presenceTracker);
        ArgumentNullException.ThrowIfNull(logger);
        _presenceTracker = presenceTracker;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Jitter: stagger replicas so their first tick doesn't coincide.
        if (StartupJitter > TimeSpan.Zero)
        {
            await Task.Delay(StartupJitter, stoppingToken);
        }

        try
        {
            using var updateTimer = new PeriodicTimer(UpdateInterval);
            do
            {
                await RefreshOnlineGaugeAsync(stoppingToken);
            }
            while (await updateTimer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task RefreshOnlineGaugeAsync(CancellationToken cancellationToken)
    {
        try
        {
            // NOTE: app_users_online is a per-instance gauge backed by shared Redis sorted
            // sets. Under horizontal scaling, aggregate with max() — not sum() — across
            // instances, because every replica reads the same Redis data and produces the
            // same value. Example PromQL: max(app_users_online)
            //
            // Phase 40.13: this is the one system-mode read in analytics-service, and it is
            // deliberately the SUM across organizations rather than a per-organization gauge.
            // The metric is operational — scraped by Prometheus, never served to a customer —
            // and an organization id as a Prometheus label would put customer identities and
            // unbounded cardinality into the metrics store to answer a question nobody asks
            // there. Per-organization counts are available only through
            // CountOnlineAsync(organizationId), which needs a tenant to call.
            var onlineUserCount = await _presenceTracker.CountOnlineAcrossAllOrganizationsAsync(cancellationToken);
            AppMetrics.UsersOnline.Set(onlineUserCount);

            // Prune on a slower cadence (compaction only — counting already filters by window).
            var now = DateTime.UtcNow;
            if (now - _lastPruneUtc >= PruneInterval)
            {
                await _presenceTracker.PruneAsync(cancellationToken);
                _lastPruneUtc = now;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Failed to refresh app_users_online gauge");
        }
    }
}
