namespace Sellevate.Learning.Infrastructure.Ai;

/// <summary>
/// Phase 40.28. ai-service's opinion on whether the material it just structured was enough to build a
/// lesson from — the half of «порог достаточности входа» a character count cannot make.
///
/// <para>
/// It arrives inside the structuring response and costs no extra call: the model forms the judgement
/// while reading the material anyway. It is an opinion and not a decision —
/// <c>ContentSufficiencyInspector</c> lets it add a refusal and never lift one.
/// </para>
/// </summary>
/// <param name="MissingCodes">
/// Codes from the closed vocabulary both services share (<c>ContentSufficiencyCodes</c> here,
/// <c>MaterialGapCodes</c> there). ai-service already drops anything outside it; unknown values that
/// arrive anyway are dropped again when the refusal is built.
/// </param>
/// <param name="Note">The model's own one-line reasoning. Diagnostic — never the customer's sentence.</param>
public sealed record AiMaterialSufficiency(
    bool IsSufficient,
    bool IsOffTopic,
    IReadOnlyList<string> MissingCodes,
    string? Note);
