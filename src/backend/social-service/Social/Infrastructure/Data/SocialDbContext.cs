using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Social.Features.Discuss.Configurations;
using Sellevate.Social.Features.Discuss.Models;
using Sellevate.Social.Features.Friends.Configurations;
using Sellevate.Social.Features.Friends.Models;
using Sellevate.Social.Identity;

namespace Sellevate.Social.Infrastructure.Data;

/// <summary>
/// The Postgres side of social-service: friendships, the discussion feed, and the read-only replica
/// of the platform's user directory.
///
/// <para>
/// Phase 40.13. Every tenant-scoped entity carries a global query filter, listed one entity at a
/// time on purpose — EF does not inherit filters through navigations, so a filter on
/// <c>DiscussThread</c> says nothing about the <c>DiscussReplies</c> hanging off it, and
/// <c>SocialTenancyModelTests</c> fails the build if an entity grows an <c>OrganizationId</c> without
/// appearing here. The filters are convenience, not security: the boundary is the row-level-security
/// policy the <c>AddOrganizationId</c> migration installs (docs/TENANCY/TENANCY.md §1.4-§1.5), and
/// every filter also admits validated platform staff.
/// </para>
///
/// <para>
/// Two entities deviate. <c>DiscussTag</c> is content rather than tenant data — a <c>NULL</c>
/// organization is the curated vocabulary every organization shares, so its filter is not plain
/// equality; without that branch a new customer would open Discuss and find no tags at all.
/// <c>UserReplica</c> carries no organization at all, because identities are cross-organization by
/// design (§4.2), the same call learning, ai and gamification made; what keeps user <em>search</em>
/// from leaking is <c>FriendService</c>, not this table.
/// </para>
///
/// <para>
/// A tenant-scoped read only sees rows while a transaction is open — see
/// <see cref="TenantTransactionScope"/>, which every service method uses as its first statement.
/// </para>
/// </summary>
public sealed class SocialDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public SocialDbContext(DbContextOptions<SocialDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Friendship> Friendships => Set<Friendship>();
    public DbSet<DiscussThread> DiscussThreads => Set<DiscussThread>();
    public DbSet<DiscussReply> DiscussReplies => Set<DiscussReply>();
    public DbSet<DiscussVote> DiscussVotes => Set<DiscussVote>();
    public DbSet<DiscussTag> DiscussTags => Set<DiscussTag>();
    public DbSet<DiscussThreadTag> DiscussThreadTags => Set<DiscussThreadTag>();
    public DbSet<DiscussPhoto> DiscussPhotos => Set<DiscussPhoto>();
    public DbSet<UserReplica> UserReplicas => Set<UserReplica>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new FriendshipEntityConfiguration());
        modelBuilder.ApplyConfiguration(new DiscussThreadConfiguration());
        modelBuilder.ApplyConfiguration(new DiscussReplyConfiguration());
        modelBuilder.ApplyConfiguration(new DiscussVoteConfiguration());
        modelBuilder.ApplyConfiguration(new DiscussTagConfiguration());
        modelBuilder.ApplyConfiguration(new DiscussThreadTagConfiguration());
        modelBuilder.ApplyConfiguration(new DiscussPhotoConfiguration());
        modelBuilder.ApplyConfiguration(new UserReplicaEntityConfiguration());

        modelBuilder.Entity<Friendship>()
            .HasQueryFilter(friendship => _tenantContext.IsPlatformWide || friendship.OrganizationId == _tenantContext.OrganizationId);

        modelBuilder.Entity<DiscussThread>()
            .HasQueryFilter(thread => _tenantContext.IsPlatformWide || thread.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<DiscussReply>()
            .HasQueryFilter(reply => _tenantContext.IsPlatformWide || reply.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<DiscussVote>()
            .HasQueryFilter(vote => _tenantContext.IsPlatformWide || vote.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<DiscussThreadTag>()
            .HasQueryFilter(threadTag => _tenantContext.IsPlatformWide || threadTag.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<DiscussPhoto>()
            .HasQueryFilter(photo => _tenantContext.IsPlatformWide || photo.OrganizationId == _tenantContext.OrganizationId);

        modelBuilder.Entity<DiscussTag>()
            .HasQueryFilter(tag =>
                _tenantContext.IsPlatformWide
                || tag.OrganizationId == null
                || tag.OrganizationId == _tenantContext.OrganizationId);
    }
}
