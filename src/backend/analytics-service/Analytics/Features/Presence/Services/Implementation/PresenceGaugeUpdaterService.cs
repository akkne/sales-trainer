using Sellevate.Analytics.Features.Presence.Services.Abstract;
using Sellevate.Analytics.Infrastructure.Metrics;

namespace Sellevate.Analytics.Features.Presence.Services.Implementation;

internal sealed class PresenceGaugeUpdaterService : BackgroundService
{
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(20);

    private readonly IPresenceTracker _presenceTracker;
    private readonly ILogger<PresenceGaugeUpdaterService> _logger;

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
            await _presenceTracker.PruneAsync(cancellationToken);
            var onlineUserCount = await _presenceTracker.CountOnlineAsync(cancellationToken);
            AppMetrics.UsersOnline.Set(onlineUserCount);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Failed to refresh app_users_online gauge");
        }
    }
}
