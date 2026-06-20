namespace Sellevate.Ai.Eventing;

public interface IDialogScoringWeightsProvider
{
    DialogScoringWeights Current { get; }

    void Update(DialogScoringWeights weights);
}
