namespace Sellevate.Ai.Eventing;

/// <summary>
/// Published when a practice conversation has been graded.
///
/// <para>
/// Phase 40.22 added the last two fields, and the reason is worth keeping. <see cref="RawScore"/>
/// does not carry the grade: it carries the pre-multiplier XP reward, and XP is bounded by the sum
/// of four configurable weights rather than by 100 — so it is uninterpretable outside ai-service and
/// says nothing about how well the conversation went. The grade the learner actually sees is
/// <c>FeedbackResult.Score</c>, on a 0–10 scale, and until 40.22 it never left this service. An
/// assignment whose completion rule is "3 диалога с оценкой ≥70" needs exactly that number, so it is
/// now published as <see cref="QualityScore"/>, normalized to 0–100 here rather than in every
/// consumer. <see cref="RawScore"/> is left alone: gamification reads it, and renaming a field on a
/// live topic buys nothing.
/// </para>
/// </summary>
/// <param name="RawScore">Pre-multiplier XP, despite the name. Not a grade — see the remarks above.</param>
/// <param name="ModeKey">
/// The human-readable dialog mode key. <see cref="ModeId"/> alone is not enough for a consumer that
/// addresses modes the way ai-service's own API does — by key — which is what an assignment's
/// <c>dialog_scenario</c> content item stores.
/// </param>
/// <param name="QualityScore">The grade shown to the learner, 0–100.</param>
public sealed record DialogEvaluatedEvent(
    Guid UserId,
    string SessionId,
    Guid BundleId,
    Guid ModeId,
    int RawScore,
    int XpEarned,
    string ModeKey,
    int QualityScore);

public sealed record GamificationDialogWeightsUpdatedEvent(
    int Confidence,
    int Structure,
    int Objection,
    int Goal,
    double Multiplier);

public sealed record UserRegisteredEvent(Guid UserId, string Email, string DisplayName, string? AvatarKey);

public sealed record UserUpdatedEvent(Guid UserId, string DisplayName, string? AvatarKey);

public sealed record UserDeletedEvent(Guid UserId);

/// <summary>
/// Phase 40.19. organization-service's <c>organization.profile.updated</c>, as this service needs
/// to read it. A local copy of the wire contract, matching every other incoming event here.
/// </summary>
public sealed record OrganizationProfileUpdatedEvent(
    Guid OrganizationId,
    string? Product,
    string? Icp,
    string? Tone,
    string ObjectionsJson,
    string ScriptJson,
    string GlossaryJson,
    string BannedClaimsJson,
    DateTime UpdatedAt);
