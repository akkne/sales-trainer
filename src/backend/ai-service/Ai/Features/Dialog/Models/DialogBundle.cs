namespace Sellevate.Ai.Features.Dialog.Models;

public sealed class DialogBundle
{
    public Guid Id { get; set; }

    /// <summary>
    /// Phase 40.11. <see langword="null"/> means the row belongs to the global dialog library
    /// shared by every organization — including the two seeded hidden bundles
    /// (<c>company-call</c>, <c>custom-scenario</c>), which stay global on purpose. A non-null
    /// value means one organization authored it (docs/TENANCY/CONTENT_MODEL.md). The security
    /// boundary is the RLS policy created by the AddOrganizationId migration; the EF query filter
    /// on this property is convenience (docs/TENANCY/TENANCY.md 1.4-1.5).
    /// </summary>
    public Guid? OrganizationId { get; set; }

    public Guid SkillId { get; set; }
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string IconEmoji { get; set; } = null!;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsHidden { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<DialogMode> Modes { get; set; } = [];
}
