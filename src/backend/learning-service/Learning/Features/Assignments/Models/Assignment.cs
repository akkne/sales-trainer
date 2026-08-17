using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Common.Constants;

namespace Sellevate.Learning.Features.Assignments.Models;

/// <summary>
/// Phase 40.21. A short, targeted, team-wide piece of practice with a deadline
/// (docs/TENANCY/ASSIGNMENTS.md §1).
///
/// <para>
/// <b>A separate entity, not a repurposed programme.</b> The skill tree — and the
/// <c>ProgramVersion</c> that freezes it — is a long, sequential, self-paced curriculum somebody
/// walks over months. An assignment is the opposite object on every axis that matters: days long,
/// aimed at named people, issued after one internal training session, and worthless once its deadline
/// has passed. Forcing the second into the first would give the programme a deadline it has no
/// meaning for and give the assignment a version-and-pin lifecycle nobody wants for a five-day task.
/// </para>
///
/// <para>
/// Strict tenant data: there is no such thing as a global assignment, so <see cref="OrganizationId"/>
/// is non-null and the row-level-security policy is plain equality — the same call 40.17 made for
/// programmes, and for the same reason. A <c>NULL</c> owner here would mean "everybody's homework".
/// </para>
///
/// <para>
/// <b>What this block does not do.</b> Nothing evaluates <see cref="CompletionRule"/> (40.22),
/// nothing resolves <see cref="Audience"/> into people or notifies them (40.23), and nothing acts on
/// <see cref="RepeatSchedule"/> (40.24). Those columns exist now because the schema is this block's
/// deliverable and adding them later would mean a second migration over a live table; their contents
/// are validated for shape and stored, never interpreted.
/// </para>
/// </summary>
public sealed class Assignment : ITenantScoped
{
    public Guid Id { get; set; }

    /// <summary>
    /// Owning organization; never null. The security boundary is the Postgres row-level-security
    /// policy created by the AddAssignments migration — the EF query filter on this property is
    /// convenience (docs/TENANCY/TENANCY.md §1.4–1.5).
    /// </summary>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// The РОП who created it. Nullable for the same reason <c>ProgramVersion.CreatedBy</c> is: 40.24
    /// re-issues assignments from a background job, and a repeat has no human pressing anything.
    /// </summary>
    public Guid? CreatedBy { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>What the team is supposed to get better at, in the РОП's own words. Shown to them, not parsed.</summary>
    public string? Goal { get; set; }

    /// <summary>One of <see cref="AssignmentSourceTypes"/>.</summary>
    public string SourceType { get; set; } = AssignmentSourceTypes.Manual;

    /// <summary>
    /// What this assignment came out of, read according to <see cref="SourceType"/>: the uploaded
    /// training material or the frozen library content it produced, or the metric that proposed it.
    /// Null exactly when the source type is <c>manual</c>, and the check constraint enforces that.
    ///
    /// <para>
    /// <b>When it names library content it names a frozen version, never a lesson.</b> A
    /// <c>lesson-version:&lt;uuid&gt;</c> reference still means in a year what it meant on the day it
    /// was written; a <c>LessonId</c> would silently re-point at whatever the lesson has become, which
    /// is precisely the defect 40.16 spent a block removing from progress records.
    /// </para>
    /// </summary>
    public string? SourceRef { get; set; }

    /// <summary>
    /// The ordered work, as canonical JSON: <c>{"items":[{"kind":…,"reference":…,"orderIndex":…}]}</c>
    /// over the vocabulary in <see cref="AssignmentContentItemKinds"/>. Stored as <c>jsonb</c>.
    ///
    /// <para>
    /// <b>References only — no exercise body ever lands here.</b> An assignment's exercises are
    /// ordinary exercises inside a pinned <c>LessonVersion</c>, so their bodies stay in the one place
    /// the platform already reads them from (<c>Exercise.SerializedContent</c>) and the existing
    /// eleven renderers play them with no new code. A second home for exercise content would be a
    /// second grading path, a second override story and a second thing for 40.19's substitution to
    /// forget.
    /// </para>
    /// </summary>
    public string Content { get; set; } = "{\"items\":[]}";

    /// <summary>
    /// Who it is for, as canonical JSON over <see cref="AssignmentAudienceKinds"/> — the rule, not the
    /// resolved people. Stored as <c>jsonb</c>. See <see cref="AssignmentAudienceKinds"/> for why
    /// learning-service cannot hold the list itself.
    /// </summary>
    public string Audience { get; set; } = "{\"kind\":\"whole_team\"}";

    /// <summary>When the assignment becomes visible to its audience. Null means "as soon as it is active".</summary>
    public DateTime? OpensAt { get; set; }

    /// <summary>When it is due. Null means open-ended, which is allowed and is usually a mistake.</summary>
    public DateTime? Deadline { get; set; }

    /// <summary>
    /// The quality threshold that decides completion, as <c>jsonb</c>. Required, and required to be a
    /// JSON object carrying a <c>kind</c> — which is the whole of what 40.21 knows about it.
    ///
    /// <para>
    /// <b>Required rather than defaulted, on purpose.</b> A default would have to be "no threshold",
    /// and an assignment that completes on a click is the compliance-theatre failure
    /// docs/TENANCY/ASSIGNMENTS.md §1.1 is written to prevent: managers click through in four
    /// minutes, the dashboard reads 100%, and the number is a lie the РОП eventually catches. Making
    /// the creator state a rule means that failure mode never has a resting place in the schema.
    /// </para>
    /// </summary>
    public string CompletionRule { get; set; } = "{}";

    /// <summary>
    /// How the assignment re-issues itself, as <c>jsonb</c>, or null for one-shot. Validated for shape
    /// (an object with a <c>kind</c>) and otherwise untouched: the schedule vocabulary and the
    /// background job that walks it are 40.24.
    /// </summary>
    public string? RepeatSchedule { get; set; }

    /// <summary>One of <see cref="AssignmentStatuses"/>.</summary>
    public string Status { get; set; } = AssignmentStatuses.Draft;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? ActivatedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    /// <summary>
    /// Phase 40.23. When the "your deadline is close" notice went out for the deadline this
    /// assignment currently has, or null if it has not.
    ///
    /// <para>
    /// <b>A column rather than a Redis marker or a recomputation.</b> The sweep has to answer "have
    /// I already told these people about this date", and the two cheaper answers both fail:
    /// recomputing from the notification inbox means learning-service reading notification-service's
    /// Redis, and a Redis flag means the fact that somebody was warned outlives or predeceases the
    /// row it is about depending on a TTL. It sits here because it is a property of the assignment.
    /// </para>
    ///
    /// <para>
    /// <b>Extending the deadline clears it</b>, which is what makes the notice describe a date
    /// rather than an event: a РОП who moves a due date is asking for the team to be told about the
    /// new one. That is also why the notification's dedupe key carries the deadline.
    /// </para>
    /// </summary>
    public DateTime? DeadlineNoticeSentAt { get; set; }

    public ICollection<AssignmentProgress> ProgressRecords { get; set; } = [];
}
