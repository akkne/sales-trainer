namespace Sellevate.Learning.Features.Reference.Models;

public sealed class ReferenceMaterial
{
    public Guid Id { get; set; }

    /// <summary>
    /// Phase 40.10. <see langword="null"/> means the row belongs to the global content library
    /// shared by every organization; a non-null value means one organization's own copy
    /// (docs/TENANCY/CONTENT_MODEL.md). Written since 40.18 by copy-on-write override creation.
    /// </summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>
    /// Phase 40.18. Set when this row is one organization's copy-on-write override of a global
    /// reference material; null otherwise. A row with a parent always has an owning organization,
    /// enforced by a database CHECK for the same reason as <c>Technique.ParentTechniqueId</c>.
    /// </summary>
    public Guid? ParentMaterialId { get; set; }

    /// <summary>
    /// Phase 40.18. Lowercase hex SHA-256 of the parent's canonical content at the moment this
    /// override was forked or last reviewed. See <c>Technique.BaseContentHash</c> for why the fork
    /// point is a fingerprint here and a version id on lessons.
    /// </summary>
    public string? BaseContentHash { get; set; }

    /// <summary>
    /// Phase 40.18. "Take the new base" retires the override rather than deleting it, so that a
    /// retired override stops shadowing its parent while the row itself survives.
    /// </summary>
    public bool IsArchived { get; set; }

    public Guid SkillId { get; set; }
    public string Title { get; set; } = "";
    public string MarkdownContent { get; set; } = "";
    public int SortOrder { get; set; }
    public string? Category { get; set; }
    public string? Tags { get; set; }
}
