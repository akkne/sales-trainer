using SalesTrainer.Api.Features.Metrics.Services.Abstract;
using SalesTrainer.Api.Infrastructure.Metrics;

namespace SalesTrainer.Api.Features.Metrics;

/// <summary>
/// Prometheus scrapes gauges, so the "online users" value must be pushed into the gauge
/// out-of-band. This service recomputes the count from Redis every tick (a little faster
/// than the 15s scrape so the value stays fresh) and prunes stale presence entries.
/// Cloned from the UpstreamConnectionWarmupService pattern.
/// </summary>
internal sealed class PresenceGaugeUpdaterService : BackgroundService
{
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(20);

    private readonly IPresenceTracker _presenceTracker;
    private readonly ILogger<PresenceGaugeUpdaterService> _logger;

    public PresenceGaugeUpdaterService(
        IPresenceTracker presenceTracker,
        ILogger<PresenceGaugeUpdaterService> logger)
    {
        _presenceTracker = presenceTracker;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var timer = new PeriodicTimer(UpdateInterval);
            do
            {
                await RefreshOnlineGaugeAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    private async Task RefreshOnlineGaugeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _presenceTracker.PruneAsync(cancellationToken);
            var online = await _presenceTracker.CountOnlineAsync(cancellationToken);
            AppMetrics.UsersOnline.Set(online);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Never let a Redis hiccup crash the background loop; just keep the last value.
            _logger.LogWarning(exception, "Failed to refresh app_users_online gauge");
        }
    }
}
