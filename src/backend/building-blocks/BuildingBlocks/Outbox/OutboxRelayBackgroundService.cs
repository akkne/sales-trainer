using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Sellevate.BuildingBlocks.Outbox;

/// <summary>
/// Hosted relay that, on a fixed interval, opens a scope, resolves the service's
/// <see cref="IOutboxStore"/> (over its EF <c>DbContext</c>) plus the shared
/// <see cref="IOutboxEventForwarder"/>, and dispatches pending outbox messages to Kafka.
/// Register one of these per producing service.
/// </summary>
public sealed class OutboxRelayBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OutboxSettings _settings;
    private readonly ILogger<OutboxRelayBackgroundService> _logger;

    public OutboxRelayBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<OutboxSettings> settings,
        ILogger<OutboxRelayBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMilliseconds(Math.Max(100, _settings.PollingIntervalMilliseconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
                var forwarder = scope.ServiceProvider.GetRequiredService<IOutboxEventForwarder>();
                var processor = new OutboxRelayProcessor(store, forwarder, _logger);

                await processor.DispatchPendingAsync(_settings.BatchSize, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Outbox relay tick failed; continuing");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
