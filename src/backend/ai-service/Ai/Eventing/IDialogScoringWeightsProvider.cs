namespace Sellevate.Ai.Eventing;

/// <summary>
/// The scoring weights currently in force, and the seam the Kafka consumer replaces them through.
/// Implementations are shared across requests and must be safe to read while <c>Update</c> runs.
/// </summary>
public interface IDialogScoringWeightsProvider
{
    DialogScoringWeights Current { get; }

    void Update(DialogScoringWeights weights);
}
