using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Social.Features.Discuss.Models;

namespace Sellevate.Social.Features.Discuss.Configurations;

public sealed class DiscussThreadConfiguration : IEntityTypeConfiguration<DiscussThread>
{
    public void Configure(EntityTypeBuilder<DiscussThread> builder)
    {
        builder.ToTable("DiscussThreads");
        builder.HasKey(thread => thread.Id);

        builder.Property(thread => thread.Title).IsRequired().HasMaxLength(300);
        builder.Property(thread => thread.Body).IsRequired().HasMaxLength(20000);

        builder.HasMany(thread => thread.Replies)
            .WithOne(reply => reply.Thread)
            .HasForeignKey(reply => reply.ThreadId)
            .OnDelete(DeleteBehavior.Cascade);

        // Phase 40.13: every list on the Discuss screen is "this organization, then sort", so the
        // organization leads each index rather than being an afterthought filter.
        builder.HasIndex(thread => new { thread.OrganizationId, thread.AuthorId });
        builder.HasIndex(thread => new { thread.OrganizationId, thread.IsPinned });
        builder.HasIndex(thread => new { thread.OrganizationId, thread.LastActivityAt });
        builder.HasIndex(thread => new { thread.OrganizationId, thread.UpvoteCount });
        builder.HasIndex(thread => new { thread.OrganizationId, thread.CreatedAt });
    }
}
