namespace Sellevate.Ai.Eventing;

public interface IDialogEventPublisher
{
    Task PublishEvaluatedAsync(DialogEvaluatedEvent payload, CancellationToken cancellationToken = default);
}
