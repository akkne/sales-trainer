using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Gamification.Features.Achievements.Models;

public sealed class UserLearningProgress : ITenantScoped
{
    public Guid UserId { get; set; }

    /// <summary>
    /// Phase 40.13. The organization that owns this row. Enforced by the query filter in
    /// <c>GamificationDbContext</c> and, authoritatively, by the row-level-security policy the
    /// AddOrganizationId migration installs. Never assigned from a request —
    /// <c>TenantSaveChangesInterceptor</c> stamps it from <c>ITenantContext</c>.
    /// </summary>
    public Guid OrganizationId { get; set; }

    public int CompletedLessonCount { get; set; }
    public bool HasCompletedAnySkill { get; set; }
    public DateTime UpdatedAt { get; set; }
}
