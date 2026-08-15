using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Features.Techniques.Models;

namespace Sellevate.Learning.Infrastructure.Data;

public sealed class UserTechniqueProgressEntityConfiguration : IEntityTypeConfiguration<UserTechniqueProgress>
{
    public void Configure(EntityTypeBuilder<UserTechniqueProgress> builder)
    {
        builder.ToTable("UserTechniqueProgress");

        builder.HasKey(progress => progress.Id);

        builder.Property(progress => progress.OrganizationId)
            .IsRequired();

        // Phase 40.10: organization first, per docs/TENANCY/TENANCY.md section 3.
        builder.HasIndex(progress => new { progress.OrganizationId, progress.UserId, progress.TechniqueId })
            .IsUnique();

        builder.HasIndex(progress => new { progress.OrganizationId, progress.UserId });
    }
}
