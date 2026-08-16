using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Social.Features.Discuss.Models;

public sealed class DiscussThreadTag : ITenantScoped
{
    public Guid Id { get; set; }

    /// <summary>
    /// Phase 40.13. The organization that owns this row. Enforced by the query filter in
    /// <c>SocialDbContext</c> and, authoritatively, by the row-level-security policy the
    /// AddOrganizationId migration installs. Never assigned from a request —
    /// <c>TenantSaveChangesInterceptor</c> stamps it from <c>ITenantContext</c>.
    /// </summary>
    public Guid OrganizationId { get; set; }

    public Guid ThreadId { get; set; }
    public Guid TagId { get; set; }

    public DiscussThread Thread { get; set; } = null!;
    public DiscussTag Tag { get; set; } = null!;
}
