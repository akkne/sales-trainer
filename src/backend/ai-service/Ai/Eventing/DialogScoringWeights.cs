namespace Sellevate.Ai.Eventing;

public sealed record DialogScoringWeights(
    int Confidence,
    int Structure,
    int Objection,
    int Goal,
    double Multiplier)
{
    public static DialogScoringWeights Default { get; } = new(25, 25, 25, 25, 1.0);
}
