namespace Sellevate.Learning.Features.ContentGeneration.Models;

/// <summary>
/// Phase 40.28. A recorded refusal: the pipeline will not generate from this, and here is precisely
/// why and what would fix it (roadmap 40.28).
///
/// <para>
/// <b>A list, not a paragraph.</b> The 40.20 screen has to show «чего не хватает» as items the РОП
/// can tick off — one of them is usually the only one they can act on today. A prose refusal reads as
/// an apology and gets skimmed.
/// </para>
/// </summary>
/// <param name="Stage">
/// Where the refusal was decided: <c>material</c> — before anything was sent to a model, from the
/// text itself; <c>structure</c> — after structuring, from what could actually be read out of it. The
/// distinction matters to the customer: the first cost them nothing, and the second means we looked
/// properly.
/// </param>
/// <param name="Gaps">What is missing. Never empty — a refusal with nothing to fix would be a bug.</param>
/// <param name="Note">
/// The model's own one-line reasoning when the refusal came from it. Diagnostic, for a developer
/// reading a run that was refused unexpectedly; the customer's text is in <paramref name="Gaps"/>.
/// </param>
public sealed record ContentInsufficiencyDto(
    string Stage,
    IReadOnlyList<ContentSufficiencyGapDto> Gaps,
    string? Note)
{
    /// <summary>Refused from the raw text, before any call was paid for.</summary>
    public const string MaterialStage = "material";

    /// <summary>Refused from the extracted structure — the honest signal, and the more expensive one.</summary>
    public const string StructureStage = "structure";
}
