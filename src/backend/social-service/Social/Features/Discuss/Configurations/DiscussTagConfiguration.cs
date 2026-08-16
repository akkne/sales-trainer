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

        // Phase 40.13. Two indexes, not one, and the second is not redundant: Postgres treats
        // NULLs in a composite unique index as distinct, so UNIQUE(OrganizationId, Slug) alone would
        // happily accept the curated tag "objections" twice at the global level. The partial index
        // is what actually keeps the shared vocabulary unique. Same pair learning-service needed for
        // Skill.IconicName in 40.10.
        builder.HasIndex(tag => new { tag.OrganizationId, tag.Slug }).IsUnique();
        builder.HasIndex(tag => tag.Slug)
            .IsUnique()
            .HasFilter("\"OrganizationId\" IS NULL")
            .HasDatabaseName("IX_DiscussTags_Slug_Global");

        builder.HasIndex(tag => new { tag.OrganizationId, tag.IsCurated });
    }
}
