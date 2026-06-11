using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesTrainer.Api.Features.Auth.Models;
using SalesTrainer.Api.Features.Discuss.Models;

namespace SalesTrainer.Api.Features.Discuss.Configurations;

public class DiscussThreadConfiguration : IEntityTypeConfiguration<DiscussThread>
{
    public void Configure(EntityTypeBuilder<DiscussThread> builder)
    {
        builder.ToTable("DiscussThreads");
        builder.HasKey(thread => thread.Id);

        builder.Property(thread => thread.Title).IsRequired().HasMaxLength(300);
        builder.Property(thread => thread.Body).IsRequired().HasMaxLength(20000);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(thread => thread.AuthorId)
            .OnDelete(DeleteBehavior.Cascade);

        // AcceptedReplyId is a soft pointer (no FK) to avoid a thread<->reply cascade cycle.
        builder.HasIndex(thread => thread.AuthorId);
        builder.HasIndex(thread => thread.IsPinned);
        builder.HasIndex(thread => thread.LastActivityAt);
        builder.HasIndex(thread => thread.UpvoteCount);
        builder.HasIndex(thread => thread.CreatedAt);
    }
}
