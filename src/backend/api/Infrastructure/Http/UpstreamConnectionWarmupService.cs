namespace SalesTrainer.Api.Infrastructure.Http;

/// <summary>
/// Re-warms upstream connections on startup and every few minutes, staying under
/// the pooled-connection idle timeout so dialog turns always hit a warm socket.
/// </summary>
internal sealed class UpstreamConnectionWarmupService : BackgroundService
{
    private static readonly TimeSpan WarmupInterval = TimeSpan.FromMinutes(4);

    private readonly UpstreamConnectionWarmup _warmup;
    private readonly ILogger<UpstreamConnectionWarmupService> _logger;

    public UpstreamConnectionWarmupService(
        UpstreamConnectionWarmup warmup,
        ILogger<UpstreamConnectionWarmupService> logger)
    {
        _warmup = warmup;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var warmedCount = await _warmup.WarmupOnceAsync(stoppingToken);
            _logger.LogInformation("Upstream connection warmup started for {TargetCount} target(s)", warmedCount);

            using var timer = new PeriodicTimer(WarmupInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await _warmup.WarmupOnceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }
}
