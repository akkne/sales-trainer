using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Features.SkillTree.Models;

namespace Sellevate.Learning.Infrastructure.Data;

public sealed class UserSkillProgressEntityConfiguration : IEntityTypeConfiguration<UserSkillProgress>
{
    public void Configure(EntityTypeBuilder<UserSkillProgress> builder)
    {
        builder.Property(progress => progress.OrganizationId)
            .IsRequired();

        // Phase 40.10: organization first, per docs/TENANCY/TENANCY.md section 3. Deliberately not
        // unique: the table had no unique constraint before this block and existing databases may
        // already hold duplicate (UserId, SkillId) rows, which a unique index would refuse.
        builder.HasIndex(progress => new { progress.OrganizationId, progress.UserId, progress.SkillId });
    }
}
