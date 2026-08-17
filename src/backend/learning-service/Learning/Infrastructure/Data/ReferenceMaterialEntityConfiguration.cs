using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Features.Reference.Models;

namespace Sellevate.Learning.Infrastructure.Data;

public sealed class ReferenceMaterialEntityConfiguration : IEntityTypeConfiguration<ReferenceMaterial>
{
    public void Configure(EntityTypeBuilder<ReferenceMaterial> builder)
    {
        builder.Property(material => material.OrganizationId);

        // Phase 40.10: organization first, per docs/TENANCY/TENANCY.md section 3.
        builder.HasIndex(material => new { material.OrganizationId, material.SkillId, material.SortOrder });

        // Phase 40.18, same shape and same reasoning as Technique.ParentTechniqueId.
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
