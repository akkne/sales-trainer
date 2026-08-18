using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Social.Features.Discuss.Models;

public sealed class DiscussPhoto : ITenantScoped
{
    public Guid Id { get; set; }

    /// <summary>
    /// Phase 40.13. The organization that owns this row. Enforced by the query filter in
    /// <c>SocialDbContext</c> and, authoritatively, by the row-level-security policy the
    /// AddOrganizationId migration installs. Never assigned from a request —
    /// <c>TenantSaveChangesInterceptor</c> stamps it from <c>ITenantContext</c>.
    /// </summary>
    public Guid OrganizationId { get; set; }

    public DiscussPhotoOwner OwnerType { get; set; }
    public Guid OwnerId { get; set; }
    public string ObjectKey { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public int OrderIndex { get; set; }
    public long SizeBytes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
