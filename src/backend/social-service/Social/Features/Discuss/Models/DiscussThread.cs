using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Social.Features.Discuss.Models;

public sealed class DiscussThread : ITenantScoped
{
    public Guid Id { get; set; }

    /// <summary>
    /// Phase 40.13. The organization that owns this row. Enforced by the query filter in
    /// <c>SocialDbContext</c> and, authoritatively, by the row-level-security policy the
    /// AddOrganizationId migration installs. Never assigned from a request —
    /// <c>TenantSaveChangesInterceptor</c> stamps it from <c>ITenantContext</c>.
    /// </summary>
    public Guid OrganizationId { get; set; }

    public Guid AuthorId { get; set; }
    public string Title { get; set; } = null!;
    public string Body { get; set; } = null!;

    public int UpvoteCount { get; set; }
    public int ReplyCount { get; set; }
    public int ViewCount { get; set; }

    public Guid? AcceptedReplyId { get; set; }

    public bool IsPinned { get; set; }
    public bool IsHot { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    public ICollection<DiscussReply> Replies { get; set; } = [];
    public ICollection<DiscussThreadTag> ThreadTags { get; set; } = [];
}
