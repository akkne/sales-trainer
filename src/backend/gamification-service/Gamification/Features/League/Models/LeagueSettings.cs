using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Gamification.Features.League.Models;

public sealed class LeagueSettings : ITenantScoped
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Phase 40.13. The organization that owns this row. Enforced by the query filter in
    /// <c>GamificationDbContext</c> and, authoritatively, by the row-level-security policy the
    /// AddOrganizationId migration installs. Never assigned from a request —
    /// <c>TenantSaveChangesInterceptor</c> stamps it from <c>ITenantContext</c>.
    /// </summary>
    public Guid OrganizationId { get; set; }

    public int MaximumLeagueParticipantCount { get; set; } = 30;
    public int PromotionZoneSize { get; set; } = 10;
    public int DemotionZoneSize { get; set; } = 5;
    public DateOnly? CurrentPeriodStartDate { get; set; }
    public DateTimeOffset? CurrentPeriodEndsAt { get; set; }
    public int PeriodLengthDays { get; set; } = 7;
}
