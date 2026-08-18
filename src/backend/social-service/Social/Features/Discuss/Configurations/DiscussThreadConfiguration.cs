using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Social.Features.Discuss.Constants;
using Sellevate.Social.Features.Discuss.Models;

namespace Sellevate.Social.Features.Discuss.Configurations;

/// <summary>
/// Phase 40.13. Every index leads with the organization, because every list on the Discuss screen is
/// "this organization, then sort" — an index that ordered first and filtered second would scan the
/// whole forum to serve one customer's page.
/// </summary>
public sealed class DiscussThreadConfiguration : IEntityTypeConfiguration<DiscussThread>
{
    public void Configure(EntityTypeBuilder<DiscussThread> builder)
    {
        builder.ToTable("DiscussThreads");
        builder.HasKey(thread => thread.Id);

        builder.Property(thread => thread.Title).IsRequired()
            .HasMaxLength(DiscussContentLimits.ThreadTitleMaximumLength);
        builder.Property(thread => thread.Body).IsRequired()
            .HasMaxLength(DiscussContentLimits.BodyMaximumLength);

        builder.HasMany(thread => thread.Replies)
            .WithOne(reply => reply.Thread)
            .HasForeignKey(reply => reply.ThreadId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(thread => new { thread.OrganizationId, thread.AuthorId });
        builder.HasIndex(thread => new { thread.OrganizationId, thread.IsPinned });
        builder.HasIndex(thread => new { thread.OrganizationId, thread.LastActivityAt });
        builder.HasIndex(thread => new { thread.OrganizationId, thread.UpvoteCount });
        builder.HasIndex(thread => new { thread.OrganizationId, thread.CreatedAt });
    }
}
