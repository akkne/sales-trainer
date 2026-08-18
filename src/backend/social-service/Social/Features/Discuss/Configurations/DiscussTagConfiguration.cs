using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Social.Features.Discuss.Constants;
using Sellevate.Social.Features.Discuss.Models;

namespace Sellevate.Social.Features.Discuss.Configurations;

/// <summary>
/// Phase 40.13. Two unique indexes on the slug, and the second is not redundant: Postgres treats
/// NULLs in a composite unique index as distinct, so <c>UNIQUE(OrganizationId, Slug)</c> alone would
/// happily accept the curated tag "objections" twice at the global level. The partial index over the
/// global rows is what actually keeps the shared vocabulary unique — the same pair learning-service
/// needed for <c>Skill.IconicName</c> in 40.10.
/// </summary>
public sealed class DiscussTagConfiguration : IEntityTypeConfiguration<DiscussTag>
{
    public void Configure(EntityTypeBuilder<DiscussTag> builder)
    {
        builder.ToTable("DiscussTags");
        builder.HasKey(tag => tag.Id);

        builder.Property(tag => tag.Slug).IsRequired()
            .HasMaxLength(DiscussContentLimits.TagSlugMaximumLength);
        builder.Property(tag => tag.Name).IsRequired()
            .HasMaxLength(DiscussContentLimits.TagNameMaximumLength);

        builder.HasMany(tag => tag.ThreadTags)
            .WithOne(threadTag => threadTag.Tag)
            .HasForeignKey(threadTag => threadTag.TagId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(tag => new { tag.OrganizationId, tag.Slug }).IsUnique();
        builder.HasIndex(tag => tag.Slug)
            .IsUnique()
            .HasFilter("\"OrganizationId\" IS NULL")
            .HasDatabaseName("IX_DiscussTags_Slug_Global");

        builder.HasIndex(tag => new { tag.OrganizationId, tag.IsCurated });
    }
}
