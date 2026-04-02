using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesTrainer.Api.Features.Lessons;

namespace SalesTrainer.Api.Infrastructure.Data;

public class UserExerciseAttemptEntityConfiguration : IEntityTypeConfiguration<UserExerciseAttempt>
{
    public void Configure(EntityTypeBuilder<UserExerciseAttempt> attemptBuilder)
    {
        attemptBuilder.Property(attempt => attempt.SerializedAnswer)
            .HasColumnType("jsonb");

        attemptBuilder.Property(attempt => attempt.SerializedAiFeedback)
            .HasColumnType("jsonb");
    }
}
