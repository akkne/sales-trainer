using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Features.SkillTree.Models;

namespace Sellevate.Learning.Infrastructure.Data;

/// <summary>
/// Maps one learner's standing on one skill. Organization first, per docs/TENANCY/TENANCY.md §3.
///
/// <para>
/// The index is <b>deliberately not unique</b>: the table had no unique constraint before 40.10 and
/// existing databases may already hold duplicate (user, skill) rows, which a unique index would refuse.
/// </para>
/// </summary>
public sealed class UserSkillProgressEntityConfiguration : IEntityTypeConfiguration<UserSkillProgress>
{
    public void Configure(EntityTypeBuilder<UserSkillProgress> builder)
    {
        builder.Property(progress => progress.OrganizationId)
            .IsRequired();

        builder.HasIndex(progress => new { progress.OrganizationId, progress.UserId, progress.SkillId });
    }
}
