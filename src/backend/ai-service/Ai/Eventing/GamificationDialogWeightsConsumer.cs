using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Idempotency;
using Sellevate.BuildingBlocks.Messaging;

namespace Sellevate.Ai.Eventing;

internal sealed class GamificationDialogWeightsConsumer : KafkaConsumerBackgroundService
{
    private readonly IDialogScoringWeightsProvider _weightsProvider;

    public GamificationDialogWeightsConsumer(
        IOptions<KafkaSettings> settings,
        IServiceScopeFactory scopeFactory,
        IIdempotencyStore idempotencyStore,
        IDialogScoringWeightsProvider weightsProvider,
        ILogger<GamificationDialogWeightsConsumer> logger)
        : base(settings, scopeFactory, idempotencyStore, logger)
    {
        _weightsProvider = weightsProvider;
    }

    protected override IReadOnlyCollection<string> Topics => [BuildingBlocks.Eventing.Topics.GamificationDialogWeightsUpdated];

    protected override Task HandleAsync(EventEnvelope envelope, IServiceProvider scopedServices, CancellationToken cancellationToken)
    {
        var payload = envelope.DataAs<GamificationDialogWeightsUpdatedEvent>();
        if (payload is null)
        {
            return Task.CompletedTask;
        }

        var multiplier = payload.Multiplier <= 0 ? 1.0 : payload.Multiplier;
        _weightsProvider.Update(new DialogScoringWeights(
            payload.Confidence,
            payload.Structure,
            payload.Objection,
            payload.Goal,
            multiplier));

        Logger.LogInformation(
            "Updated dialog scoring weights from Gamification: {Confidence}/{Structure}/{Objection}/{Goal} x{Multiplier}",
            payload.Confidence, payload.Structure, payload.Objection, payload.Goal, multiplier);

        return Task.CompletedTask;
    }
}
