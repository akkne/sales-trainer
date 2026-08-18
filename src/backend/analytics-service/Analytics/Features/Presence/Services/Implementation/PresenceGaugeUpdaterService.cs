using Microsoft.Extensions.Options;
using Sellevate.Analytics.Features.Presence.Services.Abstract;
using Sellevate.Analytics.Infrastructure.Configuration;
using Sellevate.Analytics.Infrastructure.Metrics;

namespace Sellevate.Analytics.Features.Presence.Services.Implementation;

/// <summary>
/// Pushes the platform-wide online count into the <c>app_users_online</c> gauge on a timer, because
/// Prometheus pulls and a gauge nobody writes to reports whatever it last held. The refresh interval
/// must stay shorter than the scrape interval (see <see cref="PresenceConfiguration"/>), so the value
/// scraped is at worst one tick stale.
///
/// <para>
/// <b>The gauge is per-instance, backed by shared Redis.</b> Every replica reads the same sorted sets
/// and therefore publishes the same number, so it must be aggregated with <c>max()</c> and never
/// <c>sum()</c> — summing multiplies the headcount by the replica count. Each replica also delays its
/// first tick by a random fraction of the refresh interval so the replicas do not hit Redis in
/// lockstep. Correct PromQL and the rest of the contract: docs/MONITORING.md.
/// </para>
///
/// <para>
/// Phase 40.13: this is the one deliberate system-mode read in analytics-service — the sum across
/// organizations, not a per-organization series. A customer id as a Prometheus label would put
/// customer identities and unbounded cardinality into the metrics store to answer a question that
/// belongs in a product report; per-organization counts are reachable only through
/// <see cref="IPresenceTracker.CountOnlineAsync"/>, which requires a tenant to call.
/// </para>
///
/// <para>
/// A failed refresh is logged and swallowed: presence is best-effort telemetry, and a Redis hiccup
/// must not take the host down. The loop keeps ticking.
/// </para>
/// </summary>
internal sealed class PresenceGaugeUpdaterService : BackgroundService
{
    private readonly IPresenceTracker _presenceTracker;
    private readonly ILogger<PresenceGaugeUpdaterService> _logger;
    private readonly TimeSpan _updateInterval;
    private readonly TimeSpan _pruneInterval;
    private readonly TimeSpan _startupJitter;
    private DateTime _lastPruneUtc = DateTime.MinValue;

    public PresenceGaugeUpdaterService(
        IPresenceTracker presenceTracker,
        IOptions<PresenceConfiguration> presenceConfiguration,
        ILogger<PresenceGaugeUpdaterService> logger)
    {
        ArgumentNullException.ThrowIfNull(presenceTracker);
        ArgumentNullException.ThrowIfNull(presenceConfiguration);
        ArgumentNullException.ThrowIfNull(logger);
        _presenceTracker = presenceTracker;
        _logger = logger;
        _updateInterval = TimeSpan.FromSeconds(presenceConfiguration.Value.GaugeRefreshIntervalSeconds);
        _pruneInterval = TimeSpan.FromMinutes(presenceConfiguration.Value.PruneIntervalMinutes);
        _startupJitter = TimeSpan.FromMilliseconds(
            Random.Shared.Next(0, (int)_updateInterval.TotalMilliseconds));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_startupJitter > TimeSpan.Zero)
        {
            await Task.Delay(_startupJitter, stoppingToken);
        }

        try
        {
            using var updateTimer = new PeriodicTimer(_updateInterval);
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

    /// <summary>
    /// Reads the count, sets the gauge, and compacts the Redis sets on a slower cadence. Pruning can
    /// never change a reported value — <see cref="IPresenceTracker.CountOnlineAsync"/> already filters
    /// by the presence window — so it is spaced out to avoid redundant writes on every tick.
    /// </summary>
    private async Task RefreshOnlineGaugeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var onlineUserCount = await _presenceTracker.CountOnlineAcrossAllOrganizationsAsync(cancellationToken);
            AppMetrics.UsersOnline.Set(onlineUserCount);

            var now = DateTime.UtcNow;
            if (now - _lastPruneUtc >= _pruneInterval)
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
