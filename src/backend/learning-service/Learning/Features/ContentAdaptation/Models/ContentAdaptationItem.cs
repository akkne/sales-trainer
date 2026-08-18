using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Common.Constants;

namespace Sellevate.Learning.Features.ContentAdaptation.Models;

/// <summary>
/// Phase 40.32. One exercise inside a batch: what the model proposes for it, and what a person
/// decided about that proposal. This is the row the roadmap calls «дифф», and it is the unit of
/// accept and reject.
///
/// <para>
/// <b>The item is the idempotency key of the expensive half.</b> One item is one LLM call. An item
/// that already carries an answer is never <see cref="ContentAdaptationItemStatuses.Pending"/>
/// again, so a batch of sixty exercises interrupted at item forty resumes at item forty-one — the
/// forty already paid for are not re-read. It is the same shape as
/// <c>ContentGenerationJob.ProducedLessonId</c>, moved down a level because here the money is spent
/// per row instead of per run.
/// </para>
///
/// <para>
/// <b><see cref="BaseContentHash"/> is what makes accepting safe.</b> A proposal is an answer to a
/// specific body of text. Between the proposal and the click, somebody may have edited that
/// exercise by hand — and applying a rewrite computed against the old wording would silently discard
/// their edit, which is exactly the failure 40.18 refused to build a three-way merge for. So the
/// hash of the body the model was shown travels with the proposal, and accept recomputes it. If it
/// moved, the item is stale and the answer is 409, not a merge.
/// </para>
/// </summary>
public sealed class ContentAdaptationItem : ITenantScoped
{
    public Guid Id { get; set; }

    /// <summary>Owning organization; never null. Denormalized from the batch so the RLS policy can be plain equality.</summary>
    public Guid OrganizationId { get; set; }

    public Guid JobId { get; set; }

    /// <summary>
    /// The exercise the proposal is about, as resolved at collection time. For a lesson the
    /// organization has already overridden this is <b>their</b> exercise; for a lesson they have not,
    /// it is the global library row — see <c>ContentAdaptationScopeCollector</c>, which selects
    /// through 40.18's read resolution precisely so those two cases never both appear.
    /// </summary>
    public Guid ExerciseId { get; set; }

    /// <summary>The lesson that exercise belongs to. Null owner means the global library.</summary>
    public Guid LessonId { get; set; }

    /// <summary>Human label, so a queue of sixty rows reads as content and not as identifiers.</summary>
    public string LessonTitle { get; set; } = string.Empty;

    /// <summary>One of <see cref="ExerciseTypes"/>. Frozen here: a proposal that changes the type is rejected outright.</summary>
    public string ExerciseType { get; set; } = string.Empty;

    /// <summary>Position inside its lesson, so the queue can be ordered the way the learner sees it.</summary>
    public int OrderInLesson { get; set; }

    /// <summary>
    /// SHA-256 of the canonical body the model was shown, in the convention
    /// <c>LessonVersion.ContentHash</c> established. Recomputed on accept; see the class remarks.
    /// </summary>
    public string BaseContentHash { get; set; } = string.Empty;

    /// <summary>One of <see cref="ContentAdaptationItemStatuses"/>.</summary>
    public string Status { get; set; } = ContentAdaptationItemStatuses.Pending;

    /// <summary>
    /// The rewritten body as canonical JSON, or null in review mode and before the call returns.
    /// Validated against <c>ExerciseContentValidator</c> before it is ever stored, so a proposal that
    /// reaches the queue is always a body the product can actually play.
    /// </summary>
    public string? ProposedContent { get; set; }

    /// <summary>
    /// The model's own sentence about what it changed and why — the <b>prose half of the diff</b>.
    ///
    /// <para>
    /// A diff for a person is not a JSON patch. The machine-readable half (which fields moved) is
    /// computed on the server from the two documents and travels beside this; what it cannot supply
    /// is the reason, and «заменил абстрактную выгоду на ваш срок внедрения» is what actually lets
    /// somebody answer yes or no in five seconds.
    /// </para>
    /// </summary>
    public string? ChangeSummary { get; set; }

    /// <summary>
    /// Review mode: the findings as canonical JSON — a list of <c>{code, detail}</c> drawn from
    /// <see cref="ContentReviewFindingCodes"/>. Null in rewrite mode. Non-null exactly when a review
    /// item is proposed, and the database says so (<c>CK_ContentAdaptationItems_Proposal</c>).
    /// </summary>
    public string? Findings { get; set; }

    /// <summary>
    /// The exercise the accepted proposal was actually written to, which is <b>not</b> always
    /// <see cref="ExerciseId"/>: accepting a rewrite of a global-library exercise forks the lesson
    /// first (40.18 copy-on-write), and the body lands in the organization's own copy. Recording
    /// which row was written is the only way to answer «куда это уехало» afterwards.
    /// </summary>
    public Guid? AppliedExerciseId { get; set; }

    /// <summary>Non-null exactly when <see cref="Status"/> is <c>accepted</c>; the database enforces the pair.</summary>
    public DateTime? AppliedAt { get; set; }

    /// <summary>Who answered this item. Null while it is unanswered — and no worker ever fills it in.</summary>
    public Guid? ResolvedBy { get; set; }

    public DateTime? ResolvedAt { get; set; }

    /// <summary>Attempts spent on this one exercise. Budgeted per item, so one bad exercise cannot exhaust the batch.</summary>
    public int Attempts { get; set; }

    public string? FailureReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ContentAdaptationJob? Job { get; set; }
}
