using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Social.Features.Discuss.Models;

namespace Sellevate.Social.Features.Discuss.Configurations;

public sealed class DiscussThreadTagConfiguration : IEntityTypeConfiguration<DiscussThreadTag>
{
    public void Configure(EntityTypeBuilder<DiscussThreadTag> builder)
    {
        builder.ToTable("DiscussThreadTags");
        builder.HasKey(threadTag => threadTag.Id);

        builder.HasOne(threadTag => threadTag.Thread)
            .WithMany(thread => thread.ThreadTags)
            .HasForeignKey(threadTag => threadTag.ThreadId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(threadTag => new { threadTag.ThreadId, threadTag.TagId }).IsUnique();
        builder.HasIndex(threadTag => threadTag.TagId);
    }
}
