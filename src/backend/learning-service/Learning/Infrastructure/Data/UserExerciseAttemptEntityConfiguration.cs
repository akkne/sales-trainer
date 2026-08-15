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
    }
}
