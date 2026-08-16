using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Gamification.Features.League.Models;

public sealed class League : ITenantScoped
{
    public Guid Id { get; set; }

    /// <summary>
    /// Phase 40.13. The organization that owns this row. Enforced by the query filter in
    /// <c>GamificationDbContext</c> and, authoritatively, by the row-level-security policy the
    /// AddOrganizationId migration installs. Never assigned from a request —
    /// <c>TenantSaveChangesInterceptor</c> stamps it from <c>ITenantContext</c>.
    /// </summary>
    public Guid OrganizationId { get; set; }

    public string Tier { get; set; } = "bronze";
    public DateOnly WeekStartDate { get; set; }
    public DateOnly WeekEndDate { get; set; }
}
