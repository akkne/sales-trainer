using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Social.Features.Friends.Models;

public sealed class Friendship : ITenantScoped
{
    public Guid Id { get; set; }

    /// <summary>
    /// Phase 40.13. The organization that owns this row. Enforced by the query filter in
    /// <c>SocialDbContext</c> and, authoritatively, by the row-level-security policy the
    /// AddOrganizationId migration installs. Never assigned from a request —
    /// <c>TenantSaveChangesInterceptor</c> stamps it from <c>ITenantContext</c>.
    /// </summary>
    public Guid OrganizationId { get; set; }

    public Guid RequesterId { get; set; }
    public Guid AddresseeId { get; set; }
    public FriendshipStatus Status { get; set; } = FriendshipStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcceptedAt { get; set; }

    // Computed (stored) columns — populated by the database, never set by application code.
    // They enforce that (A,B) and (B,A) cannot coexist via a unique index on (LowId, HighId).
    public Guid CanonicalLowId { get; private set; }
    public Guid CanonicalHighId { get; private set; }
}
