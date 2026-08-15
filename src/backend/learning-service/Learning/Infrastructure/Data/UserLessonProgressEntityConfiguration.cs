using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Features.Lessons.Models;

namespace Sellevate.Learning.Infrastructure.Data;

public sealed class UserLessonProgressEntityConfiguration : IEntityTypeConfiguration<UserLessonProgress>
{
    public void Configure(EntityTypeBuilder<UserLessonProgress> builder)
    {
        builder.Property(progress => progress.OrganizationId)
            .IsRequired();

        // Phase 40.10: organization first, per docs/TENANCY/TENANCY.md section 3. Same
        // no-unique-constraint reasoning as UserSkillProgressEntityConfiguration.
        builder.HasIndex(progress => new { progress.OrganizationId, progress.UserId, progress.LessonId });
    }
}
