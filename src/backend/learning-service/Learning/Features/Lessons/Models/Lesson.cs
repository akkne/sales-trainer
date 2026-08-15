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

    public Guid TopicId { get; set; }
    public int OrderInTopic { get; set; }
    public string Title { get; set; } = "";

    public Topic? Topic { get; set; }
}
