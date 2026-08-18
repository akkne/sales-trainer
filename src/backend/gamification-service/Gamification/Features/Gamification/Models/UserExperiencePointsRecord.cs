using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Gamification.Features.Gamification.Models;

public sealed class UserExperiencePointsRecord : ITenantScoped
{
    public Guid Id { get; set; }

    /// <summary>
    /// Phase 40.13. The organization that owns this row. Enforced by the query filter in
    /// <c>GamificationDbContext</c> and, authoritatively, by the row-level-security policy the
    /// AddOrganizationId migration installs. Never assigned from a request —
    /// <c>TenantSaveChangesInterceptor</c> stamps it from <c>ITenantContext</c>.
    /// </summary>
    public Guid OrganizationId { get; set; }

    public Guid UserId { get; set; }
    public int Amount { get; set; }
    public string Source { get; set; } = string.Empty;
    public DateTime EarnedAt { get; set; }

    /// <summary>
    /// The EventId of the integration event that triggered this grant.
    /// Null for direct/non-event grants. Used for DB-level idempotency.
    /// </summary>
    public Guid? SourceEventId { get; set; }
}
