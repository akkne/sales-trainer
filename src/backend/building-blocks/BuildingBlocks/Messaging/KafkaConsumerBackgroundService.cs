using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Idempotency;
using Sellevate.BuildingBlocks.Tenancy;

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
    /// <summary>
    /// How long the consume loop pauses after an unhandled error around the handler, so a broker or
    /// Redis outage becomes a slow retry rather than a tight spin against a failing dependency.
    /// </summary>
    private static readonly TimeSpan UnhandledErrorBackoff = TimeSpan.FromSeconds(1);

    private readonly KafkaSettings _settings;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IIdempotencyStore _idempotencyStore;

    /// <summary>Logger for the concrete consumer (passed through from the subclass).</summary>
    protected ILogger Logger { get; }

    /// <summary>The topics this consumer subscribes to.</summary>
    protected abstract IReadOnlyCollection<string> Topics { get; }

    /// <summary>
    /// Whether every event this consumer handles must carry an <see cref="EventEnvelope.OrganizationId"/>.
    /// <c>true</c> by default: a tenant-scoped consumer must never silently process a message
    /// with no tenant context (docs/TENANCY/TENANCY.md §1.6/§1.7). Override to <c>false</c> only
    /// for a consumer that is genuinely platform-global by design (e.g. replicating Identity's
    /// cross-org user directory) — this must be an explicit, auditable opt-in, never the default
    /// reaction to an empty context.
    /// </summary>
    protected virtual bool RequiresOrganization => true;

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

    /// <summary>
    /// Hands the consume loop to a dedicated background thread. Confluent's <c>Consume()</c> is a
    /// blocking call, so running it inline would stall the host's startup/shutdown thread.
    /// </summary>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => ConsumeLoop(stoppingToken), stoppingToken);
    }

    /// <summary>
    /// Consumes until cancellation, committing an offset only once its message is accounted for.
    ///
    /// <para>
    /// The outer <c>catch</c> around <see cref="ProcessMessageAsync"/> is load-bearing and must not
    /// be narrowed. Handler exceptions are already caught by the processor's retry path; what this
    /// catches is everything <em>around</em> the handler — the Redis idempotency store, the offset
    /// commit, and the scope's service resolution. None of that was guarded, and .NET's default
    /// <c>BackgroundServiceExceptionBehavior</c> is <c>StopHost</c>, so a five-second Redis restart
    /// or a routine consumer-group rebalance took the whole service down, HTTP API included, and
    /// <c>restart:</c> turned that into a crash loop. This base class backs thirteen consumers
    /// across the platform.
    /// </para>
    ///
    /// <para>
    /// An <see cref="OperationCanceledException"/> reaching the outer handler is a normal shutdown,
    /// not a failure, and is deliberately absorbed without logging.
    /// </para>
    /// </summary>
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
                catch (ConsumeException consumeException)
                {
                    Logger.LogError(consumeException, "Kafka consume error in group '{Group}'", _settings.ConsumerGroupId);
                    continue;
                }

                if (result?.Message is null)
                {
                    continue;
                }

                try
                {
                    await ProcessMessageAsync(consumer, result, stoppingToken);
                }
                catch (Exception exception)
                {
                    Logger.LogError(
                        exception,
                        "Unhandled error processing a Kafka message in group '{Group}'; continuing", _settings.ConsumerGroupId);

                    await Task.Delay(UnhandledErrorBackoff, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
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
        var tenantContext = scope.ServiceProvider.GetRequiredService<TenantContext>();

        var processor = new EventMessageProcessor(
            _settings.ConsumerGroupId, _idempotencyStore, deadLetterPublisher, resilience, Logger);

        var outcome = await processor.ProcessAsync(
            result.Topic,
            result.Message.Key,
            result.Message.Value,
            (envelope, cancellationToken) =>
            {
                ApplyTenantContext(tenantContext, envelope, RequiresOrganization, GetType().Name);
                return HandleAsync(envelope, scope.ServiceProvider, cancellationToken);
            },
            stoppingToken);

        if (outcome != MessageProcessingOutcome.Redeliver)
        {
            consumer.Commit(result);
        }
    }

    /// <summary>
    /// Sets <paramref name="tenantContext"/> from <paramref name="envelope"/> before the handler
    /// runs. Throws if the envelope carries no organization and <paramref name="requiresOrganization"/>
    /// is <c>true</c> — the exception surfaces through the handler's retry/dead-letter path exactly
    /// like any other handler failure, so a message can never be processed silently without a
    /// tenant. Safe to call again on a retry: re-setting the same organization, or re-entering
    /// system mode, is a no-op on an already-set <see cref="TenantContext"/>.
    /// </summary>
    internal static void ApplyTenantContext(
        TenantContext tenantContext, EventEnvelope envelope, bool requiresOrganization, string consumerName)
    {
        if (envelope.OrganizationId is { } organizationId)
        {
            tenantContext.SetOrganization(organizationId);
            return;
        }

        if (requiresOrganization)
        {
            throw new InvalidOperationException(
                $"Event {envelope.Type} ({envelope.EventId}) carries no organization, but consumer '{consumerName}' requires one.");
        }

        tenantContext.EnterSystemMode();
    }
}
