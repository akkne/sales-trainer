using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Idempotency;

namespace Sellevate.BuildingBlocks.Messaging;

/// <summary>
/// Reusable base for an idempotent Kafka consumer. Subclasses declare which
/// <see cref="Topics"/> to subscribe to and implement <see cref="HandleAsync"/>;
/// this base owns the consume loop, JSON envelope parsing, per-event idempotency
/// dedupe (via <see cref="IIdempotencyStore"/>) and manual offset commit.
///
/// <para>
/// Offsets are committed only after a message is successfully handled (or skipped
/// as a duplicate), giving at-least-once delivery. A handler that throws is retried
/// in-process up to <see cref="ConsumerResilienceSettings.MaxHandlerRetries"/> times; if it
/// still fails and dead-lettering is enabled, the original message is forwarded to
/// <c>&lt;topic&gt;.dlt</c> and the offset is committed so a poison message can never block
/// the partition. Combined with idempotency this is the standard "process, then commit"
/// pattern with a bounded retry + dead-letter escape hatch.
/// </para>
/// </summary>
public abstract class KafkaConsumerBackgroundService : BackgroundService
{
    private readonly KafkaSettings _settings;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IIdempotencyStore _idempotencyStore;

    /// <summary>Logger for the concrete consumer (passed through from the subclass).</summary>
    protected ILogger Logger { get; }

    /// <summary>The topics this consumer subscribes to.</summary>
    protected abstract IReadOnlyCollection<string> Topics { get; }

    protected KafkaConsumerBackgroundService(
        IOptions<KafkaSettings> settings,
        IServiceScopeFactory scopeFactory,
        IIdempotencyStore idempotencyStore,
        ILogger logger)
    {
        _settings = settings.Value;
        _scopeFactory = scopeFactory;
        _idempotencyStore = idempotencyStore;
        Logger = logger;
    }

    /// <summary>
    /// Handle one already-deduplicated event. A scope is created per message so the
    /// handler can resolve scoped services (e.g. a DbContext) from
    /// <paramref name="scopedServices"/>. Throwing causes a redelivery.
    /// </summary>
    protected abstract Task HandleAsync(
        EventEnvelope envelope,
        IServiceProvider scopedServices,
        CancellationToken cancellationToken);

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Confluent's Consume() blocks; run the loop on a dedicated background thread
        // so we never stall the host's startup/shutdown thread.
        return Task.Run(() => ConsumeLoop(stoppingToken), stoppingToken);
    }

    private async Task ConsumeLoop(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _settings.BootstrapServers,
            GroupId = _settings.ConsumerGroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(Topics);
        Logger.LogInformation(
            "Kafka consumer '{Group}' subscribed to: {Topics}",
            _settings.ConsumerGroupId, string.Join(", ", Topics));

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? result;
                try
                {
                    result = consumer.Consume(stoppingToken);
                }
                catch (ConsumeException ex)
                {
                    Logger.LogError(ex, "Kafka consume error in group '{Group}'", _settings.ConsumerGroupId);
                    continue;
                }

                if (result?.Message is null)
                {
                    continue;
                }

                await ProcessMessageAsync(consumer, result, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        finally
        {
            consumer.Close();
        }
    }

    private async Task ProcessMessageAsync(
        IConsumer<string, string> consumer,
        ConsumeResult<string, string> result,
        CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var deadLetterPublisher = scope.ServiceProvider.GetRequiredService<IDeadLetterPublisher>();
        var resilience = scope.ServiceProvider.GetRequiredService<IOptions<ConsumerResilienceSettings>>().Value;

        var processor = new EventMessageProcessor(
            _settings.ConsumerGroupId, _idempotencyStore, deadLetterPublisher, resilience, Logger);

        var outcome = await processor.ProcessAsync(
            result.Topic,
            result.Message.Key,
            result.Message.Value,
            (envelope, cancellationToken) => HandleAsync(envelope, scope.ServiceProvider, cancellationToken),
            stoppingToken);

        if (outcome != MessageProcessingOutcome.Redeliver)
        {
            consumer.Commit(result);
        }
    }
}
