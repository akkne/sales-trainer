using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Learning.Features.Techniques.Models;

public sealed class UserTechniqueProgress : ITenantScoped
{
    public Guid Id { get; set; }

    /// <summary>
    /// Phase 40.10. Owning organization; never null. The security boundary is the Postgres
    /// row-level-security policy created by the AddOrganizationId migration — the EF query
    /// filter on this property is convenience (docs/TENANCY/TENANCY.md 1.4-1.5).
    /// </summary>
    public Guid OrganizationId { get; set; }

    public Guid UserId { get; set; }
    public Guid TechniqueId { get; set; }
    public int Level { get; set; }
    public int MasteryPercent { get; set; }
    public int PracticeCount { get; set; }
    public DateTime? LastPracticedAt { get; set; }
    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;
}
