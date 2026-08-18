using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Common.Constants;

namespace Sellevate.Learning.Features.ContentGeneration.Models;

/// <summary>
/// Phase 40.27. One run of the admin content pipeline: material in, a structure a human confirms,
/// then a lesson (roadmap 40.27).
///
/// <para>
/// <b>Strict tenant data.</b> There is no such thing as a global generation run: the material is one
/// customer's product deck and the structure is their objections, their script and their compliance
/// list. So <see cref="OrganizationId"/> is non-null and the row-level-security policy is plain
/// equality — never the content policy's <c>IS NULL OR = current</c>, which on this table would mean
/// one customer's uploaded deck was readable by every other. The content the run produces is a
/// different matter and is content-flavoured, because it is a lesson; it is nonetheless always
/// owned by this organization and never global (docs/SEEDER.md §0 — the shared library has exactly
/// one authoring path and it is not this one).
/// </para>
///
/// <para>
/// <b>The state machine is the cost control.</b> Whether a run has been paid for is derived from
/// the columns rather than counted: a structure exists exactly when <see cref="Structure"/> is
/// non-null, a lesson exists exactly when <see cref="ProducedLessonId"/> is non-null, and the worker
/// refuses both halves when the answer is already there. That is 40.22's rule — derive state, never
/// increment a counter — applied where the counter would have been denominated in money.
/// </para>
/// </summary>
public sealed class ContentGenerationJob : ITenantScoped
{
    public Guid Id { get; set; }

    /// <summary>Owning organization; never null. See the class remarks for why the policy is equality.</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>The РОП who started the run. Nullable only so a future non-human starter has somewhere to be.</summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>What the run is about, in the РОП's words. Becomes the generated topic's title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The raw material, verbatim. Kept after structuring rather than discarded, because the second
    /// question a reviewer asks at the checkpoint is «откуда это взялось» and the answer has to be
    /// readable. It is never sent to the generation call — see
    /// <see cref="Sellevate.Learning.Infrastructure.Ai.AiGenerateExercisesRequest"/>.
    /// </summary>
    public string SourceMaterial { get; set; } = string.Empty;

    /// <summary>One of <see cref="ContentGenerationJobStatuses"/>.</summary>
    public string Status { get; set; } = ContentGenerationJobStatuses.Structuring;

    /// <summary>
    /// The extracted structure as canonical JSON, or null before the first LLM call returns. Stored
    /// as <c>jsonb</c>, same convention as <c>Assignment.Content</c> and <c>Exercise.SerializedContent</c>.
    ///
    /// <para>
    /// <b>It is overwritten by the reviewer's edit, and no history is kept.</b> The document exists
    /// to be corrected in thirty seconds; a version chain on a disposable draft would be machinery
    /// nobody reads, and what is worth freezing — the content the run finally produced — is frozen
    /// properly, as a <c>LessonVersion</c> snapshot.
    /// </para>
    /// </summary>
    public string? Structure { get; set; }

    public DateTime? StructuredAt { get; set; }

    /// <summary>
    /// Phase 40.28. How much of <see cref="SourceMaterial"/> the last structuring call actually read.
    ///
    /// <para>
    /// <b>This is what makes a refusal arguable without being expensive.</b> A run refused for thin
    /// material is meant to be answered by adding more of it, and starting the whole run over would
    /// re-pay for structuring the deck that was already read. So new material is appended, this
    /// marker says where the paid-for part ends, and the next call is sent only the tail — together
    /// with the structure already extracted, which the prompt is told to keep rather than rewrite.
    /// The customer pays for what they added and nothing else.
    /// </para>
    /// </summary>
    public int StructuredMaterialLength { get; set; }

    /// <summary>
    /// Phase 40.28. The recorded refusal as canonical JSON — a list of gaps, each with a code and the
    /// sentence the РОП reads — or null when the run has nothing to complain about.
    ///
    /// <para>
    /// Non-null exactly when <see cref="Status"/> is <c>insufficient</c>, and the database says so
    /// (<c>CK_ContentGenerationJobs_Insufficiency</c>). It is cleared when the run moves on, because a
    /// stale «не хватает возражений» sitting on a run that is now generating would be read as a
    /// warning about the lesson it is about to produce.
    /// </para>
    /// </summary>
    public string? Insufficiency { get; set; }

    /// <summary>
    /// When a human said the structure was right. This is the checkpoint, recorded: nothing is
    /// generated while it is null, and it is the only transition in the pipeline a background worker
    /// cannot make on its own.
    /// </summary>
    public DateTime? ApprovedAt { get; set; }

    public Guid? ApprovedBy { get; set; }

    /// <summary>
    /// The lesson this run produced, or null if it has not produced one. <b>Also the idempotency key
    /// of the expensive half</b>: a job holding a lesson id is never generated again, whatever its
    /// status, so a re-approval, a duplicated tick or a crash between the LLM call and the commit
    /// cannot bill the customer twice for the same lesson.
    /// </summary>
    public Guid? ProducedLessonId { get; set; }

    /// <summary>
    /// The frozen snapshot of what was generated (docs/TENANCY/CONTENT_MODEL.md §2.1). Generated
    /// content lands in the versioning schema like every other lesson rather than beside it — an
    /// exercise whose answer key nobody can reconstruct a year later is the defect 40.16 spent a
    /// block removing.
    /// </summary>
    public Guid? ProducedLessonVersionId { get; set; }

    /// <summary>
    /// How many exercises survived <c>ExerciseContentValidator</c>. Worth a column because "the model
    /// returned twelve and two were malformed" and "the model returned ten" look identical afterwards,
    /// and the first is a prompt problem somebody should see.
    /// </summary>
    public int ProducedExerciseCount { get; set; }

    public DateTime? GeneratedAt { get; set; }

    /// <summary>Why the last attempt failed, for the person looking at a stuck run. Null while things are fine.</summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// Attempts spent in the current half. Reset when a person approves or retries, because those are
    /// the two moments the work changes rather than repeats.
    /// </summary>
    public int Attempts { get; set; }

    /// <summary>
    /// When a worker took this job, or null when it is free. Stamped and committed <b>before</b> the
    /// LLM call, so a process that dies mid-call leaves the job visibly claimed and the lease — not a
    /// retry loop — is what releases it. A minutes-long call cannot be held inside a transaction, so
    /// this column is what stands in for one.
    /// </summary>
    public DateTime? ClaimedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
