using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Features.Lessons.Models;

namespace Sellevate.Learning.Infrastructure.Data;

public sealed class UserExerciseAttemptEntityConfiguration : IEntityTypeConfiguration<UserExerciseAttempt>
{
    public void Configure(EntityTypeBuilder<UserExerciseAttempt> builder)
    {
        builder.Property(attempt => attempt.SerializedAnswer)
            .HasColumnType("jsonb");

        builder.Property(attempt => attempt.SerializedAiFeedback)
            .HasColumnType("jsonb");

        builder.Property(attempt => attempt.OrganizationId)
            .IsRequired();

        // Phase 40.10: organization first, per docs/TENANCY/TENANCY.md section 3.
        builder.HasIndex(attempt => new { attempt.OrganizationId, attempt.UserId, attempt.ExerciseId });

        // Phase 40.16. What the accuracy series reads: every attempt of one organization grouped by
        // the snapshot it was scored against. Declared here but NOT created by the migration —
        // this is a live progress table, so the build goes to
        // docs/TENANCY/sql/40.16_progress_version_indexes_concurrently.sql, exactly as 40.10 did
        // with every index on this table. The model snapshot therefore describes an index the
        // database does not have until a human runs that script; on a fresh database that costs
        // nothing but a sequential scan.
        builder.HasIndex(attempt => new { attempt.OrganizationId, attempt.LessonVersionId, attempt.ExerciseId });
    }
}
