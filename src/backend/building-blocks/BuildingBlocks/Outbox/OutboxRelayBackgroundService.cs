using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.BuildingBlocks.Outbox;

/// <summary>
/// Hosted relay that, on a fixed interval, opens a scope, resolves the service's
/// <see cref="IOutboxStore"/> (over its EF <c>DbContext</c>) plus the shared
/// <see cref="IOutboxEventForwarder"/>, and dispatches pending outbox messages to Kafka.
/// Register one of these per producing service.
///
/// <para>
/// <b>Tenancy: system mode, declared.</b> This is the one component the tenancy design names as a
/// legitimate reader of every organization's rows (docs/TENANCY/TENANCY.md §1.6/§1.7) — which is
/// precisely why the tenant travels inside <see cref="OutboxMessage.Payload"/> rather than being
/// re-derived here at publish time. The relay therefore calls
/// <see cref="TenantContext.EnterSystemMode"/> on every tick's scope. It reads the same rows it
/// read before that call existed; what changes is that the mode is now stated instead of inferred
/// from a <see cref="TenantContext"/> that merely happened to be blank. An unset tenant is an
/// exception, never a licence — so the one place the licence is real has to say so out loud, and a
/// service that ever gives this relay a tenant-scoped scope by mistake now fails loudly.
/// </para>
/// </summary>
public sealed class OutboxRelayBackgroundService : BackgroundService
{
    /// <summary>
    /// Floor applied to <see cref="OutboxSettings.PollingIntervalMilliseconds"/>, so a
    /// misconfigured (or zero) interval degrades to a fast poll rather than to a busy loop that
    /// pins a core and hammers the database.
    /// </summary>
    private const int MinimumPollingIntervalMilliseconds = 100;

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

    /// <summary>
    /// Polls until cancellation. Each tick opens a fresh scope and declares system mode on it
    /// <b>before</b> the <c>DbContext</c> is resolved, never after: the connection interceptor
    /// decides whether to emit <c>SET LOCAL app.organization_id</c> the first time a transaction
    /// opens on that context, so the mode has to be settled while the scope is still empty.
    ///
    /// <para>
    /// A failed tick is logged and the loop continues — a transient database or broker outage must
    /// not take the relay down, since the unsent rows are still there on the next tick.
    /// </para>
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMilliseconds(
            Math.Max(MinimumPollingIntervalMilliseconds, _settings.PollingIntervalMilliseconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                scope.ServiceProvider.GetRequiredService<TenantContext>().EnterSystemMode();

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
