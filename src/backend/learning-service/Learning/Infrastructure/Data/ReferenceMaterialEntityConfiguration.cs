using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Features.Reference.Models;

namespace Sellevate.Learning.Infrastructure.Data;

/// <summary>
/// Maps reference material attached to a skill. Organization first, per docs/TENANCY/TENANCY.md §3, and a
/// parent pointer of the same shape and reasoning as <c>Technique.ParentTechniqueId</c> (40.18).
/// </summary>
public sealed class ReferenceMaterialEntityConfiguration : IEntityTypeConfiguration<ReferenceMaterial>
{
    public void Configure(EntityTypeBuilder<ReferenceMaterial> builder)
    {
        builder.Property(material => material.OrganizationId);

        builder.HasIndex(material => new { material.OrganizationId, material.SkillId, material.SortOrder });

        builder.HasOne<ReferenceMaterial>()
            .WithMany()
            .HasForeignKey(material => material.ParentMaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(material => material.BaseContentHash)
            .HasMaxLength(64);

        builder.Property(material => material.IsArchived)
            .IsRequired()
            .HasDefaultValue(false);

        builder.HasIndex(material => material.ParentMaterialId);
    }
}
