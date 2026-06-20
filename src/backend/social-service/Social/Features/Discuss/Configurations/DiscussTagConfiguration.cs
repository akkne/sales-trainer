using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Social.Features.Discuss.Models;

namespace Sellevate.Social.Features.Discuss.Configurations;

public sealed class DiscussTagConfiguration : IEntityTypeConfiguration<DiscussTag>
{
    public void Configure(EntityTypeBuilder<DiscussTag> builder)
    {
        builder.ToTable("DiscussTags");
        builder.HasKey(tag => tag.Id);

        builder.Property(tag => tag.Slug).IsRequired().HasMaxLength(60);
        builder.Property(tag => tag.Name).IsRequired().HasMaxLength(60);

        builder.HasMany(tag => tag.ThreadTags)
            .WithOne(threadTag => threadTag.Tag)
            .HasForeignKey(threadTag => threadTag.TagId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(tag => tag.Slug).IsUnique();
        builder.HasIndex(tag => tag.IsCurated);
    }
}
