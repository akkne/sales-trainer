using Sellevate.Learning.Common.Constants;

namespace Sellevate.Learning.Features.Lessons.Models;

/// <summary>
/// Phase 40.15. An immutable snapshot of one lesson — its title plus its full ordered set of
/// exercises — taken at publication time (docs/TENANCY/CONTENT_MODEL.md §2.1, §2.2).
///
/// <para>
/// The versioned unit is deliberately the whole lesson and not the individual <c>Exercise</c> row:
/// a lesson is kilobytes, and versioning each exercise separately would turn every historical
/// question ("what did this learner actually answer?") into a reconstruction from N version rows.
/// <c>Exercise</c> rows stay the working representation the admin panel edits; this table is what
/// history reads.
/// </para>
///
/// <para>
/// Exactly one row per lesson may sit in <see cref="LessonVersionStatuses.Draft"/>, enforced by a
/// partial unique index rather than by application code, because two admins editing at once is
/// precisely the case application code loses. Everything else is frozen: a database trigger
/// refuses to change <see cref="Content"/>, <see cref="ContentHash"/>, <see cref="VersionNumber"/>,
/// <see cref="LessonId"/> or <see cref="PublishedAt"/> once the row has left draft.
/// </para>
/// </summary>
public sealed class LessonVersion
{
    public Guid Id { get; set; }

    /// <summary>
    /// Denormalized copy of the owning <see cref="Lesson"/>'s organization: <see langword="null"/>
    /// for the global library, an organization id for that organization's own copy. Denormalized
    /// on purpose — a row-level-security policy can only compare columns of the row it is
    /// filtering, so the isolation boundary needs the value here and not one join away
    /// (docs/TENANCY/TENANCY.md §1.5). A lesson never changes owner, so the copy cannot drift.
    /// </summary>
    public Guid? OrganizationId { get; set; }

    public Guid LessonId { get; set; }

    /// <summary>Monotonic per lesson, starting at 1. Unique together with <see cref="LessonId"/>.</summary>
    public int VersionNumber { get; set; }

    /// <summary>
    /// The snapshot, as canonical JSON produced by <c>LessonSnapshotSerializer</c>. Stored as
    /// <c>jsonb</c>, which means Postgres re-normalizes it on write: what a <c>SELECT</c> returns is
    /// equivalent to, but not byte-identical with, what was hashed. <see cref="ContentHash"/> is
    /// defined over the canonical form the serializer produces, never over the bytes the database
    /// hands back.
    /// </summary>
    public string Content { get; set; } = "{}";

    /// <summary>
    /// Lowercase hex SHA-256 of the canonical <see cref="Content"/>, UTF-8 encoded. Exists so that
    /// pressing "publish" without having changed anything does not mint a version — a chain of
    /// identical versions would make the 40.16 accuracy series step for no reason.
    /// </summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>One of <see cref="LessonVersionStatuses"/>.</summary>
    public string Status { get; set; } = LessonVersionStatuses.Draft;

    /// <summary>
    /// Which version of the parent (global) lesson this override was forked from, or
    /// <see langword="null"/> for a lesson that is nobody's override. Provenance only: 40.18 uses
    /// it to mark an override stale when the base publishes something newer, and is allowed to
    /// re-point it — which is why the freeze trigger deliberately leaves this column writable.
    /// </summary>
    public Guid? BaseVersionId { get; set; }

    /// <summary>
    /// Set by the publisher: cosmetic edit (<see langword="false"/>) or semantic one
    /// (<see langword="true"/> — the correct answer or the grading criteria moved). 40.16 joins the
    /// metric series across cosmetic versions and splits it across breaking ones.
    /// </summary>
    public bool IsBreaking { get; set; }

    /// <summary>The administrator who opened the draft. Null only for rows created by a background path.</summary>
    public Guid? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? PublishedAt { get; set; }

    public Lesson? Lesson { get; set; }
}
