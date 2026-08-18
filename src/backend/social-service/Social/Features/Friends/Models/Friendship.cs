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

    /// <summary>
    /// The lower of the two user ids, and <see cref="CanonicalHighId"/> the higher. Stored computed
    /// columns: the database populates them, application code never assigns them, and a unique index
    /// over the pair is what stops (A,B) and (B,A) from both existing.
    /// </summary>
    public Guid CanonicalLowId { get; private set; }

    /// <inheritdoc cref="CanonicalLowId"/>
    public Guid CanonicalHighId { get; private set; }
}
