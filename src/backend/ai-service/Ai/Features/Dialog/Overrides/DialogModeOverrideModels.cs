namespace Sellevate.Ai.Features.Dialog.Overrides;

/// <summary>
/// Phase 40.18. One prompt override, with staleness derived on read rather than stored — the same
/// rule learning-service follows, and for the same reason: a flag that is computed cannot disagree
/// with the facts, and no marking pass has to fan out into tenants the publisher is not in.
/// </summary>
public sealed record DialogModeOverrideDto(
    Guid OverrideId,
    Guid BaseModeId,
    Guid BundleId,
    string Key,
    string Title,
    bool IsStale,
    string? ForkedFromHash,
    string? BaseCurrentHash);

/// <summary>
/// Phase 40.18. What the review screen compares. Two prompts and no merge: an automatic three-way
/// merge of a system prompt produces something that reads like a prompt and then coaches a
/// salesperson according to a rule nobody wrote.
/// </summary>
public sealed record DialogModeOverrideReviewDto(
    DialogModeOverrideDto Summary,
    string OverrideChatSystemPrompt,
    string OverrideFeedbackSystemPrompt,
    string? BaseChatSystemPrompt,
    string? BaseFeedbackSystemPrompt);

public enum DialogModeOverrideOutcome
{
    Created,
    AlreadyExists,
    SourceNotFound,
    SourceNotGlobal,

    /// <summary>
    /// The mode belongs to a hidden seeded bundle (<c>company-call</c>, <c>custom-scenario</c>).
    /// Those two stay global by product decision and by the shape of the code that drives them:
    /// their prompts are assembled from placeholders the service fills in, and a per-organization
    /// copy would drift away from the code that feeds it (docs/TENANCY/CONTENT_MODEL.md §4).
    /// </summary>
    SourceIsSeededHiddenMode,

    NoOrganization,
}

public sealed record DialogModeOverrideResult(DialogModeOverrideOutcome Outcome, DialogModeOverrideDto? Override);
