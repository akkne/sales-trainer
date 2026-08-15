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
    }
}
