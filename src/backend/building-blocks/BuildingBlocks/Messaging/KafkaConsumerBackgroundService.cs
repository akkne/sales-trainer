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
/// as a duplicate), giving at-least-once delivery. A handler that throws is logged
/// and the offset is NOT committed, so the message is redelivered — combined with
/// idempotency this is the standard "process then commit" pattern.
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
        EventEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<EventEnvelope>(result.Message.Value, EventEnvelope.JsonOptions);
        }
        catch (JsonException ex)
        {
            // A malformed message can never succeed on redelivery — commit past it.
            Logger.LogError(ex, "Skipping unparseable message on {Topic}", result.Topic);
            consumer.Commit(result);
            return;
        }

        if (envelope is null)
        {
            consumer.Commit(result);
            return;
        }

        try
        {
            if (await _idempotencyStore.HasProcessedAsync(_settings.ConsumerGroupId, envelope.EventId, stoppingToken))
            {
                Logger.LogDebug(
                    "Skipping duplicate event {EventId} ({Type}) in group '{Group}'",
                    envelope.EventId, envelope.Type, _settings.ConsumerGroupId);
            }
            else
            {
                using var scope = _scopeFactory.CreateScope();
                await HandleAsync(envelope, scope.ServiceProvider, stoppingToken);
                // Mark only after a successful handle: a throwing handler leaves the
                // event unmarked and uncommitted, so it is retried on redelivery.
                await _idempotencyStore.MarkProcessedAsync(_settings.ConsumerGroupId, envelope.EventId, stoppingToken);
            }

            consumer.Commit(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Do NOT commit: the message will be redelivered. Idempotency makes the
            // retry safe even if the handler partially succeeded.
            Logger.LogError(
                ex, "Handler failed for event {EventId} ({Type}); will redeliver",
                envelope.EventId, envelope.Type);
        }
    }
}
