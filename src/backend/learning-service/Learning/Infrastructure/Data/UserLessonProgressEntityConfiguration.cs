using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Features.Lessons.Models;

namespace Sellevate.Learning.Infrastructure.Data;

/// <summary>
/// Maps one learner's standing on one lesson. Organization first, per docs/TENANCY/TENANCY.md §3, with
/// the same no-unique-constraint reasoning as <see cref="UserSkillProgressEntityConfiguration"/>.
///
/// <para>
/// Phase 40.16. The second index answers "how many of my team completed this version of the lesson", per
/// organization. Same deal as <c>UserExerciseAttempts</c>: declared here, built by
/// docs/TENANCY/sql/40.16_progress_version_indexes_concurrently.sql rather than by the migration, because
/// this table grows with usage.
/// </para>
/// </summary>
public sealed class UserLessonProgressEntityConfiguration : IEntityTypeConfiguration<UserLessonProgress>
{
    public void Configure(EntityTypeBuilder<UserLessonProgress> builder)
    {
        builder.Property(progress => progress.OrganizationId)
            .IsRequired();

        builder.HasIndex(progress => new { progress.OrganizationId, progress.UserId, progress.LessonId });

        builder.HasIndex(progress => new { progress.OrganizationId, progress.LessonVersionId });
    }
}
