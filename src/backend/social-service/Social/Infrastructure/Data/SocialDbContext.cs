using Microsoft.EntityFrameworkCore;
using Sellevate.Social.Features.Discuss.Configurations;
using Sellevate.Social.Features.Discuss.Models;
using Sellevate.Social.Features.Friends.Configurations;
using Sellevate.Social.Features.Friends.Models;
using Sellevate.Social.Identity;

namespace Sellevate.Social.Infrastructure.Data;

public sealed class SocialDbContext : DbContext
{
    public SocialDbContext(DbContextOptions<SocialDbContext> options) : base(options)
    {
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
    }
}
