using Microsoft.Extensions.Options;
using Sellevate.Ai.Infrastructure.Configuration;

namespace Sellevate.Ai.Infrastructure.Http;

/// <summary>
/// Re-warms upstream connections on startup and every few minutes, staying under
/// the pooled-connection idle timeout so dialog turns always hit a warm socket.
///
/// <para>
/// <b>Every failure is swallowed on purpose.</b> This service re-opens idle sockets; it is a latency
/// optimisation and nothing depends on it. Only <see cref="OperationCanceledException"/> was caught
/// before, so an <c>OptionsValidationException</c> out of target resolution — a typo in provider
/// configuration — escaped <c>ExecuteAsync</c> and, with .NET's default <c>StopHost</c> behaviour,
/// crash-looped the whole of ai-service over a purely optional warmup. Review, 40.34.
/// </para>
/// </summary>
internal sealed class UpstreamConnectionWarmupService : BackgroundService
{
    private readonly UpstreamConnectionWarmup _warmup;
    private readonly IOptions<UpstreamWarmupConfiguration> _warmupOptions;
    private readonly ILogger<UpstreamConnectionWarmupService> _logger;

    public UpstreamConnectionWarmupService(
        UpstreamConnectionWarmup warmup,
        IOptions<UpstreamWarmupConfiguration> warmupOptions,
        ILogger<UpstreamConnectionWarmupService> logger)
    {
        _warmup = warmup;
        _warmupOptions = warmupOptions;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var warmedCount = await _warmup.WarmupOnceAsync(stoppingToken);
            _logger.LogInformation("Upstream connection warmup started for {TargetCount} target(s)", warmedCount);

            using var timer = new PeriodicTimer(
                TimeSpan.FromMinutes(_warmupOptions.Value.IntervalMinutes));
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
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Upstream connection warmup stopped; upstream calls will open sockets on demand");
        }
    }
}
