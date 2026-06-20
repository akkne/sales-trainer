using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Messaging;

namespace Sellevate.Ai.Eventing;

internal sealed class KafkaDialogEventPublisher(IEventPublisher eventPublisher) : IDialogEventPublisher
{
    public Task PublishEvaluatedAsync(DialogEvaluatedEvent payload, CancellationToken cancellationToken = default) =>
        eventPublisher.PublishAsync(
            Topics.DialogEvaluated, payload.UserId.ToString(), Topics.DialogEvaluated, payload, cancellationToken: cancellationToken);
}
