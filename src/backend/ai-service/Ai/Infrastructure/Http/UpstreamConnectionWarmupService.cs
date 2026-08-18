namespace Sellevate.Ai.Infrastructure.Http;

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
        // Every failure is swallowed on purpose. This service re-opens idle sockets; it is a latency
        // optimisation and nothing depends on it. Only OperationCanceledException was caught before,
        // so an OptionsValidationException out of ResolveTargets — a typo in provider configuration —
        // escaped to ExecuteAsync and, with .NET's default StopHost behaviour, crash-looped the whole
        // ai-service over a purely optional warmup. Review, 40.34.
        try
        {
            var warmedCount = await _warmup.WarmupOnceAsync(stoppingToken);
            _logger.LogInformation("Upstream connection warmup started for {TargetCount} target(s)", warmedCount);

            using var timer = new PeriodicTimer(WarmupInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await _warmup.WarmupOnceAsync(stoppingToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    _logger.LogWarning(exception, "Upstream connection warmup tick failed; continuing");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Upstream connection warmup stopped; upstream calls will open sockets on demand");
        }
    }
}
