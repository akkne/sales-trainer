namespace Sellevate.Learning.Features.Content.Models;

/// <summary>
/// Phase 40.18. The three content families the roadmap names. The kind travels in the route rather
/// than the body, so a review action can never be aimed at a row of a different family by editing a
/// payload.
/// </summary>
public static class ContentOverrideKinds
{
    public const string Lesson = "lessons";
    public const string Technique = "techniques";
    public const string ReferenceMaterial = "reference-materials";

    public static bool IsKnown(string kind)
        => kind is Lesson or Technique or ReferenceMaterial;
}

/// <summary>
/// Phase 40.18. One row of the organization's override list, and — when <see cref="IsStale"/> is
/// true — one entry of its review queue.
///
/// <para>
/// Staleness is <b>derived on read</b>, never stored. There is no flag to set, so there is no flag
/// to be wrong: no marking transaction has to fan out across every organization at publish time
/// (which the RLS write clause would refuse anyway, since a publisher cannot write into tenants it
/// is not in), no background sweep can lag behind, and no row can claim it is current when its base
/// has moved. The queue is a query, and the three review actions resolve it by changing the facts it
/// reads (docs/DECISIONS.md, 2026-08-18).
/// </para>
/// </summary>
/// <param name="Kind">One of <see cref="ContentOverrideKinds"/>.</param>
/// <param name="OverrideId">The organization's own row.</param>
/// <param name="BaseId">The global row it was forked from.</param>
/// <param name="Title">Human label, so a review queue reads as content and not as identifiers.</param>
/// <param name="IsStale">The base has moved since this override was forked or last reviewed.</param>
/// <param name="ForkedFrom">
/// Lesson overrides: the id of the <c>LessonVersion</c> forked from. Techniques and reference
/// materials: the base's content hash at fork time. Null means the fork point was never recorded,
/// which counts as stale as soon as the base has anything published — "unknown base, needs review".
/// </param>
/// <param name="BaseCurrent">
/// The same shape of pointer, describing the base as it is now. Null when the base has never been
/// published (lessons only), which is the one case where an override cannot be stale.
/// </param>
public sealed record ContentOverrideDto(
    string Kind,
    Guid OverrideId,
    Guid BaseId,
    string Title,
    bool IsStale,
    string? ForkedFrom,
    string? BaseCurrent);

/// <summary>
/// Phase 40.18. What the review screen needs to show for one override: what changed upstream, what
/// the organization changed, and nothing pre-merged.
///
/// <para>
/// <b>There is no merge and no textual diff here, deliberately and per the roadmap.</b> The payload
/// is three documents and the screen decides how to display them. Content is prose and grading
/// criteria; a three-way merge of those produces plausible-looking nonsense that then grades a
/// salesperson, and a server-side diff would be the first step down that road.
/// </para>
/// </summary>
/// <param name="Override">The organization's current content.</param>
/// <param name="BaseAtFork">
/// The base as it was when the override was forked — available for lessons, because 40.15 froze
/// every published version and the fork point is a pointer at one that still exists. Null for
/// techniques and reference materials: their fork point is a fingerprint, and the text it
/// fingerprinted was overwritten in place by whoever edited the base.
/// </param>
/// <param name="BaseCurrent">The base as it is now.</param>
public sealed record ContentOverrideReviewDto(
    ContentOverrideDto Summary,
    System.Text.Json.JsonElement? Override,
    System.Text.Json.JsonElement? BaseAtFork,
    System.Text.Json.JsonElement? BaseCurrent);

/// <summary>Phase 40.18. Why a copy-on-write request did or did not produce a copy.</summary>
public enum ContentOverrideOutcome
{
    /// <summary>A fresh copy was made.</summary>
    Created,

    /// <summary>The organization already had one; the existing copy is returned untouched.</summary>
    AlreadyExists,

    /// <summary>No such row, or not visible to this organization. The caller answers 404.</summary>
    SourceNotFound,

    /// <summary>The row is already somebody's copy, not part of the global library.</summary>
    SourceNotGlobal,

    /// <summary>
    /// The caller has no organization — platform staff, or a request that reached the service
    /// without the gateway's header. There is nobody for the copy to belong to.
    /// </summary>
    NoOrganization,
}

public sealed record ContentOverrideResult(ContentOverrideOutcome Outcome, ContentOverrideDto? Override);
