using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Learning.Features.SkillTree.Models;

public sealed class UserSkillProgress : ITenantScoped
{
    public Guid Id { get; set; }

    /// <summary>
    /// Phase 40.10. Owning organization; never null. The security boundary is the Postgres
    /// row-level-security policy created by the AddOrganizationId migration — the EF query
    /// filter on this property is convenience (docs/TENANCY/TENANCY.md 1.4-1.5).
    /// </summary>
    public Guid OrganizationId { get; set; }

    public Guid UserId { get; set; }
    public Guid SkillId { get; set; }
    public string Status { get; set; } = "locked";
    public int CompletedLessonCount { get; set; }
    public int TotalLessonCount { get; set; }
}
