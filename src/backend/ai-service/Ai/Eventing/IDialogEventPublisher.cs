namespace Sellevate.Ai.Eventing;

/// <summary>
/// Publishes the "a dialog was graded" fact the rest of the platform reacts to. Failure to publish must
/// not fail the grading it describes.
/// </summary>
public interface IDialogEventPublisher
{
    Task PublishEvaluatedAsync(DialogEvaluatedEvent payload, CancellationToken cancellationToken = default);
}
