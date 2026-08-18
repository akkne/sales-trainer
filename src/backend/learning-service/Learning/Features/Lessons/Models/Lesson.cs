using Sellevate.Learning.Features.SkillTree.Models;

namespace Sellevate.Learning.Features.Lessons.Models;

public sealed class Lesson
{
    public Guid Id { get; set; }

    /// <summary>
    /// Phase 40.10. <see langword="null"/> means the row belongs to the global content library
    /// shared by every organization; a non-null value means one organization's own copy
    /// (docs/TENANCY/CONTENT_MODEL.md). Nothing writes a non-null value yet — content authoring
    /// is superadmin-only until 40.18 — but the query filter and the RLS policy already read it.
    /// </summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>
    /// Phase 40.15. Set when this row is one organization's override of a global lesson; null for
    /// a lesson that stands on its own (docs/TENANCY/CONTENT_MODEL.md §1). Nothing writes it yet —
    /// copy-on-write override creation is 40.18 — but the column, its foreign key and the index
    /// exist so that block adds behaviour rather than schema.
    /// </summary>
    public Guid? ParentLessonId { get; set; }

    /// <summary>
    /// Phase 40.15. Stable identifier of the lesson within its organization, unique per
    /// <c>(OrganizationId, Slug)</c> plus a partial unique index over the global rows — Postgres
    /// treats NULLs in a composite unique index as distinct, so the composite index alone would let
    /// two global lessons share a slug (docs/TENANCY/TENANCY.md §1.9).
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Phase 40.15. Retired lessons stay in the table because published versions and historical
    /// progress point at them; archiving hides the lesson from authoring and from the tree instead
    /// of deleting the lifeline out from under its own history.
    /// </summary>
    public bool IsArchived { get; set; }

    public Guid TopicId { get; set; }
    public int OrderInTopic { get; set; }
    public string Title { get; set; } = "";

    public Topic? Topic { get; set; }
    public Lesson? ParentLesson { get; set; }
}
