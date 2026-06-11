using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesTrainer.Api.Features.Discuss.Models;

namespace SalesTrainer.Api.Features.Discuss.Configurations;

public class DiscussThreadTagConfiguration : IEntityTypeConfiguration<DiscussThreadTag>
{
    public void Configure(EntityTypeBuilder<DiscussThreadTag> builder)
    {
        builder.ToTable("DiscussThreadTags");
        builder.HasKey(threadTag => threadTag.Id);

        builder.HasOne(threadTag => threadTag.Thread)
            .WithMany(thread => thread.ThreadTags)
            .HasForeignKey(threadTag => threadTag.ThreadId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(threadTag => threadTag.Tag)
            .WithMany(tag => tag.ThreadTags)
            .HasForeignKey(threadTag => threadTag.TagId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(threadTag => new { threadTag.ThreadId, threadTag.TagId }).IsUnique();
        builder.HasIndex(threadTag => threadTag.TagId);
    }
}
