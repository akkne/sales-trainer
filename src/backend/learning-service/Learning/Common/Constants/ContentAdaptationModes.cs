namespace Sellevate.Learning.Common.Constants;

/// <summary>
/// Phase 40.32. What a batch of per-exercise proposals is <i>about</i> — the roadmap block's two
/// halves, expressed as a column rather than as two parallel machines.
///
/// <para>
/// <b>Both halves are the same machine, and that is the block's central decision.</b> «Перепиши все
/// упражнения этапа "закрытие" под наш тон» and «проверь, что РОП написал руками» differ in the
/// prompt they send and in what an item carries back; they do not differ in anything else. Both
/// select a stage's worth of exercises, both spend one LLM call per exercise, both must survive a
/// process dying halfway through a sixty-item batch, both must never touch live content on their
/// own, and both end in a queue a person walks item by item. Building that twice would have meant a
/// second lease protocol, a second claim <c>UPDATE</c>, a second background worker in
/// docs/TENANCY/BACKGROUND_JOBS.md and a second place to get idempotency subtly wrong.
/// </para>
///
/// <para>
/// What the mode does decide is the one asymmetry that matters: a <see cref="ToneRewrite"/> item
/// carries a proposed body and can therefore be <b>applied</b>, while a <see cref="QualityReview"/>
/// item carries a list of findings and has nothing to apply — accepting one is refused, and the
/// database refuses it too (<c>CK_ContentAdaptationItems_Proposal</c>).
/// </para>
/// </summary>
public static class ContentAdaptationModes
{
    /// <summary>
    /// «Перепиши под наш продукт и тон.» One proposed exercise body per item, plus the sentence
    /// explaining what was changed. Applying it is a human act, one item at a time.
    /// </summary>
    public const string ToneRewrite = "tone_rewrite";

    /// <summary>
    /// Phase 40.32's second half: reviewing what a РОП wrote by hand. One list of findings per item,
    /// drawn from the closed vocabulary in <see cref="ContentReviewFindingCodes"/>. Nothing is
    /// proposed and nothing can be applied — the fix is an edit in the ordinary editor, because a
    /// model that both diagnoses and silently repairs is a model nobody checks.
    /// </summary>
    public const string QualityReview = "quality_review";

    public static readonly string[] All = [ToneRewrite, QualityReview];

    public static bool IsKnown(string? mode) => mode is not null && All.Contains(mode);
}
