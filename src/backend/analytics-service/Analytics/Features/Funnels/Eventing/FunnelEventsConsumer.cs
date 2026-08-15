using Microsoft.Extensions.Options;
using Sellevate.Analytics.Features.Funnels.Services.Abstract;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Idempotency;
using Sellevate.BuildingBlocks.Messaging;

namespace Sellevate.Analytics.Features.Funnels.Eventing;

internal sealed class FunnelEventsConsumer : KafkaConsumerBackgroundService
{
    private readonly IFunnelEventRecorder _funnelEventRecorder;

    public FunnelEventsConsumer(
        IOptions<KafkaSettings> settings,
        IServiceScopeFactory serviceScopeFactory,
        IIdempotencyStore idempotencyStore,
        IFunnelEventRecorder funnelEventRecorder,
        ILogger<FunnelEventsConsumer> logger)
        : base(settings, serviceScopeFactory, idempotencyStore, logger)
    {
        ArgumentNullException.ThrowIfNull(funnelEventRecorder);
        _funnelEventRecorder = funnelEventRecorder;
    }

    protected override IReadOnlyCollection<string> Topics =>
    [
        BuildingBlocks.Eventing.Topics.UserRegistered,
        BuildingBlocks.Eventing.Topics.ExerciseCompleted,
        BuildingBlocks.Eventing.Topics.XpGranted,
    ];

    /// <summary>
    /// Phase 40.13: platform-global, declared rather than defaulted.
    ///
    /// <para>
    /// This consumer stores nothing. Every branch of <c>IFunnelEventRecorder</c> increments a
    /// process-local Prometheus counter and returns; there is no database, no Redis key, and no
    /// per-tenant state that could be written under the wrong organization. What the counters
    /// measure is the platform's own funnel — registrations, exercises, XP — which is operational
    /// telemetry behind <c>/metrics</c>, never served to a customer.
    /// </para>
    ///
    /// <para>
    /// Adding the organization as a Prometheus label was considered and rejected: it would put
    /// customer identities into the metrics store and make cardinality grow with the customer list,
    /// to answer a question that belongs in a product report rather than in Prometheus. Recorded in
    /// docs/DECISIONS.md.
    /// </para>
    ///
    /// <para>
    /// The opt-out also matters for delivery: <c>user.registered</c> is a cross-organization
    /// identity event (TENANCY.md §4.2) and can legitimately arrive with no organization. With the
    /// inherited default of <c>true</c> those messages would be retried and dead-lettered, so the
    /// funnel would quietly stop counting registrations — the failure would show up as a flat graph,
    /// not as an error.
    /// </para>
    /// </summary>
    protected override bool RequiresOrganization => false;

    protected override Task HandleAsync(
        EventEnvelope envelope,
        IServiceProvider scopedServices,
        CancellationToken cancellationToken)
    {
        var wasRecorded = _funnelEventRecorder.Record(envelope);
        if (!wasRecorded)
        {
            Logger.LogDebug("Ignored funnel event {EventId} of type {Type}", envelope.EventId, envelope.Type);
        }

        return Task.CompletedTask;
    }
}
