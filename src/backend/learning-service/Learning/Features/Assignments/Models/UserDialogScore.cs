using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Learning.Features.Assignments.Models;

/// <summary>
/// Phase 40.22. One graded practice conversation, as learning-service heard about it on
/// <c>dialog.evaluated</c>.
///
/// <para>
/// <b>Why a row and not a counter.</b> The roadmap's first completion rule is "3 диалога с оценкой
/// ≥70", and no single number can answer it: "how many conversations cleared 70" is a question about
/// a set. Keeping the set means <c>AssignmentProgress.AttemptCount</c> and <c>BestScore</c> are
/// <i>derived</i> on every evaluation rather than incremented, which is also what makes redelivery
/// harmless. Kafka is at-least-once and the Redis idempotency store forgets after its TTL, so a
/// counter would eventually drift upward on its own — and "tried 4 times, did not reach the bar" is
/// the single most consequential line on the РОП's screen. A number that inflates while nobody
/// practises is worse than no number.
/// </para>
///
/// <para>
/// <b>Not keyed to an assignment, on purpose.</b> The row records what happened to a person, not
/// what it counted towards. One conversation may satisfy two assignments that reference the same
/// scenario, and an assignment issued tomorrow can still be judged against nothing but the
/// conversations held after it was issued. The evaluator therefore matches on
/// <see cref="DialogModeKey"/> and <see cref="EvaluatedAt"/> rather than on a foreign key, and one
/// event writes exactly one row however many assignments care about it.
/// </para>
///
/// <para>
/// Strict tenant data: a practice conversation happens inside one organization, so
/// <see cref="OrganizationId"/> is non-null and the row-level-security policy is plain equality.
/// </para>
/// </summary>
public sealed class UserDialogScore : ITenantScoped
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>
    /// ai-service's session identifier, a string because that is what the event carries and what
    /// Mongo assigns. Unique per organization and user, which is what makes reprocessing the same
    /// event a no-op instead of a second attempt.
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// The dialog mode key, which is how an assignment's <c>dialog_scenario</c> content item names a
    /// scenario. Deliberately the key and not <see cref="DialogModeId"/>: a key is what a РОП picks
    /// and what survives an override, since 40.18's copy-on-write override of a global mode keeps
    /// its parent's key and gets a new id.
    /// </summary>
    public string DialogModeKey { get; set; } = string.Empty;

    /// <summary>The mode's row id, kept for tracing back to ai-service. Never matched on.</summary>
    public Guid DialogModeId { get; set; }

    /// <summary>The grade the learner was shown, normalized to 0–100 by ai-service.</summary>
    public int Score { get; set; }

    public DateTime EvaluatedAt { get; set; }
}
