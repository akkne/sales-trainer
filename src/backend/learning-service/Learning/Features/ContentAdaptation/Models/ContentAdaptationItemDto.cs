using System.Text.Json;

namespace Sellevate.Learning.Features.ContentAdaptation.Models;

/// <summary>
/// Phase 40.32. Everything the review screen needs to answer yes or no about one exercise.
///
/// <para>
/// <b>Three things, and nothing pre-merged.</b> The body as it stands, the body as proposed, and the
/// list of leaves that differ. The screen decides how to render them. This is deliberately the same
/// shape 40.18 chose for its override review payload and for the same stated reason: a server-side
/// merge of prose and grading criteria produces plausible nonsense which then grades a living
/// salesperson, and a merged document on this DTO would be the first step down that road.
/// </para>
///
/// <para>
/// <see cref="IsStale"/> is computed on read, never stored — the hash of the current body against
/// the hash the proposal was computed from. A stale item cannot be accepted; the answer is a 409 and
/// a re-run, not a reconciliation.
/// </para>
/// </summary>
/// <param name="CurrentContent">The exercise body as it is right now, re-read at request time.</param>
/// <param name="ProposedContent">The rewritten body. Null in review mode and while the item is pending.</param>
/// <param name="Changes">Which leaves differ. Empty in review mode.</param>
/// <param name="Findings">What is wrong with the exercise. Empty in rewrite mode.</param>
/// <param name="IsStale">The exercise has been edited since the proposal was computed.</param>
public sealed record ContentAdaptationItemDto(
    ContentAdaptationItemSummaryDto Summary,
    JsonElement? CurrentContent,
    JsonElement? ProposedContent,
    IReadOnlyList<ContentFieldChangeDto> Changes,
    IReadOnlyList<ContentReviewFindingDto> Findings,
    bool IsStale);
