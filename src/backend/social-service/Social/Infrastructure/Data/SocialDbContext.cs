using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Social.Features.Discuss.Configurations;
using Sellevate.Social.Features.Discuss.Models;
using Sellevate.Social.Features.Friends.Configurations;
using Sellevate.Social.Features.Friends.Models;
using Sellevate.Social.Identity;

namespace Sellevate.Social.Infrastructure.Data;

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

        // Phase 40.13. Convenience, not security — the boundary is the RLS policy the
        // AddOrganizationId migration installs (docs/TENANCY/TENANCY.md §1.4-§1.5).
        //
        // Every tenant-scoped entity is listed one by one, because EF does not inherit query
        // filters through navigations: a filter on DiscussThread says nothing about the
        // DiscussReplies hanging off it. SocialTenancyModelTests walks the model and fails the
        // build if an entity grows an OrganizationId without appearing here.
        //
        // Friendship first, because it is the one this whole block exists for. A manager at
        // customer A must not be able to find, friend or message somebody at customer B, and the
        // friendship row is where that is decided — ChatService refuses to open a conversation
        // between two people who are not accepted friends, so a friendship that cannot cross the
        // boundary is also a conversation that cannot.
        modelBuilder.Entity<Friendship>()
            .HasQueryFilter(friendship => friendship.OrganizationId == _tenantContext.OrganizationId);

        modelBuilder.Entity<DiscussThread>()
            .HasQueryFilter(thread => thread.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<DiscussReply>()
            .HasQueryFilter(reply => reply.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<DiscussVote>()
            .HasQueryFilter(vote => vote.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<DiscussThreadTag>()
            .HasQueryFilter(threadTag => threadTag.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<DiscussPhoto>()
            .HasQueryFilter(photo => photo.OrganizationId == _tenantContext.OrganizationId);

        // The one content-flavour filter in this database: NULL means the curated vocabulary every
        // organization shares. NOT plain equality — a new customer would otherwise open Discuss and
        // find no tags at all.
        modelBuilder.Entity<DiscussTag>()
            .HasQueryFilter(tag =>
                tag.OrganizationId == null || tag.OrganizationId == _tenantContext.OrganizationId);

        // UserReplicas carries no organization: identities are cross-organization by design
        // (TENANCY.md §4.2), the same call learning, ai and gamification made. What keeps user
        // SEARCH from leaking is not this table but FriendService, which now joins through
        // Friendships and the membership half — see SearchUsersAsync.
    }
}
