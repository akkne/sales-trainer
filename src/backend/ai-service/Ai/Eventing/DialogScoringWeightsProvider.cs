namespace Sellevate.Ai.Eventing;

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
