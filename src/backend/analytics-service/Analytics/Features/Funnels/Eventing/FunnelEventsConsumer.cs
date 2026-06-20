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
