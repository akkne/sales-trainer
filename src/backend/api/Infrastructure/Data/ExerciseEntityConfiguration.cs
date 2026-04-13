using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesTrainer.Api.Features.Lessons.Models;

namespace SalesTrainer.Api.Infrastructure.Data;

public class ExerciseEntityConfiguration : IEntityTypeConfiguration<Exercise>
{
    public void Configure(EntityTypeBuilder<Exercise> exerciseBuilder)
    {
        exerciseBuilder.Property(exercise => exercise.SerializedContent)
            .HasColumnType("jsonb");
    }
}
