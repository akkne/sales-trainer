namespace Sellevate.Learning.Features.SkillTree.Models;

public sealed class Topic
{
    public Guid Id { get; set; }

    /// <summary>
    /// Phase 40.10. <see langword="null"/> means the row belongs to the global content library
    /// shared by every organization; a non-null value means one organization's own copy
    /// (docs/TENANCY/CONTENT_MODEL.md). Nothing writes a non-null value yet — content authoring
    /// is superadmin-only until 40.18 — but the query filter and the RLS policy already read it.
    /// </summary>
    public Guid? OrganizationId { get; set; }

    public Guid SkillId { get; set; }
    public string IconicName { get; set; } = "";
    public int OrderInSkill { get; set; }
    public string Title { get; set; } = "";

    public Skill? Skill { get; set; }
}
