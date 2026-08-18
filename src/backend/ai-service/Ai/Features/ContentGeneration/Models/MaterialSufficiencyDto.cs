namespace Sellevate.Ai.Features.ContentGeneration.Models;

/// <summary>
/// Phase 40.28. The model's opinion on whether the material it just read is enough to build a lesson
/// from — «порог достаточности входа» (roadmap 40.28).
///
/// <para>
/// <b>It rides the structuring call and costs nothing extra.</b> The obvious design is a second,
/// cheap "is this about sales?" call before the expensive one. It was rejected: the structuring call
/// already reads the whole material and already forms the judgement as a side effect of extracting
/// nothing from it. Asking for the verdict in the same completion is free, cannot disagree with the
/// structure it came with, and removes an entire round trip from the path a РОП waits on
/// (docs/DECISIONS.md, 2026-08-18).
/// </para>
///
/// <para>
/// <b>It is an opinion, not a decision.</b> learning-service decides, and it treats this verdict as
/// able to <i>add</i> a refusal but never to lift one: a model that says "выглядит достаточно" over a
/// structure with no objections and no script stages must not wave the run through. That asymmetry is
/// what keeps the threshold from being bypassed by a confident completion.
/// </para>
/// </summary>
/// <param name="IsSufficient">
/// The model's answer to «хватит ли этого на четыре хороших упражнения». False is a refusal it is
/// asked to justify through <paramref name="MissingCodes"/>.
/// </param>
/// <param name="IsOffTopic">
/// The one judgement a character count cannot make: this material is not about selling. Separate from
/// <paramref name="IsSufficient"/> because the answer to it is not «добавьте ещё» — it is «это не тот
/// файл».
/// </param>
/// <param name="MissingCodes">
/// What is missing, from <see cref="MaterialGapCodes"/>. Anything outside that list is dropped before
/// it leaves this service.
/// </param>
/// <param name="Note">
/// One short sentence in the model's own words, for the log and for a human debugging a refusal.
/// <b>Never shown to the customer</b> — the customer-facing sentence is learning-service's, per code.
/// </param>
public sealed record MaterialSufficiencyDto(
    bool IsSufficient,
    bool IsOffTopic,
    IReadOnlyList<string> MissingCodes,
    string? Note)
{
    /// <summary>
    /// What a completion that said nothing about sufficiency means. Deliberately "sufficient": a
    /// missing verdict is a prompt-following failure, and refusing a customer's material because our
    /// own model forgot a field would be a refusal about us, phrased as one about them. The structure
    /// check in learning-service still runs, so an empty structure is still refused.
    /// </summary>
    public static MaterialSufficiencyDto Sufficient { get; } = new(
        IsSufficient: true,
        IsOffTopic: false,
        MissingCodes: [],
        Note: null);
}
