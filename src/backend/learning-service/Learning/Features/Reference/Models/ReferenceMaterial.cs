namespace Sellevate.Learning.Features.Reference.Models;

public sealed class ReferenceMaterial
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
    public string Title { get; set; } = "";
    public string MarkdownContent { get; set; } = "";
    public int SortOrder { get; set; }
    public string? Category { get; set; }
    public string? Tags { get; set; }
}
