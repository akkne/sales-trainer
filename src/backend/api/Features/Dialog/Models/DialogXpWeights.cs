namespace SalesTrainer.Api.Features.Dialog.Models;

/// <summary>
/// Per-criterion maximum points injected into the dialog feedback prompt. These tell
/// the AI how many XP each aspect of the call is worth; their sum is the raw 0-N score
/// the AI returns. Sourced from the admin-editable gamification settings.
/// </summary>
public sealed record DialogXpWeights(int Confidence, int Structure, int Objection, int Goal)
{
    /// <summary>The historic 25/25/25/25 split (raw score range 0-100).</summary>
    public static DialogXpWeights Default { get; } = new(25, 25, 25, 25);

    public int Total => Confidence + Structure + Objection + Goal;
}
