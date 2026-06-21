using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Idempotency;
using Sellevate.BuildingBlocks.Messaging;
using Sellevate.Gamification.Features.Gamification.Services.Abstract;

namespace Sellevate.Gamification.Eventing;

internal sealed class DialogEvaluatedConsumer : KafkaConsumerBackgroundService
{
    public DialogEvaluatedConsumer(
        IOptions<KafkaSettings> settings,
        IServiceScopeFactory scopeFactory,
        IIdempotencyStore idempotencyStore,
        ILogger<DialogEvaluatedConsumer> logger)
        : base(settings, scopeFactory, idempotencyStore, logger)
    {
    }

    protected override IReadOnlyCollection<string> Topics =>
    [
        BuildingBlocks.Eventing.Topics.DialogEvaluated,
    ];

    protected override async Task HandleAsync(EventEnvelope envelope, IServiceProvider scopedServices, CancellationToken cancellationToken)
    {
        var payload = envelope.DataAs<DialogEvaluatedEvent>();
        if (payload is null)
        {
            return;
        }

        var eventHandler = scopedServices.GetRequiredService<IGamificationEventHandler>();
        await eventHandler.HandleDialogEvaluatedAsync(payload.UserId, payload.XpEarned, envelope.EventId, cancellationToken);
    }
}
