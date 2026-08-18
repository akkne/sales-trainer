namespace Sellevate.Ai.Eventing;

/// <summary>
/// Holds the scoring weights gamification-service last published, for the whole process.
///
/// <para>
/// Registered as a singleton and written from a Kafka consumer while dialog requests read it, so the
/// field is <c>volatile</c> and replaced wholesale rather than mutated. Reads never block and never see a
/// half-updated weight set. Until the first event arrives the defaults are in force, which is why a
/// service that has never heard from gamification still scores dialogs.
/// </para>
/// </summary>
internal sealed class DialogScoringWeightsProvider : IDialogScoringWeightsProvider
{
    private volatile DialogScoringWeights _current = DialogScoringWeights.Default;

    public DialogScoringWeights Current => _current;

    public void Update(DialogScoringWeights weights)
    {
        ArgumentNullException.ThrowIfNull(weights);
        _current = weights;
    }
}
