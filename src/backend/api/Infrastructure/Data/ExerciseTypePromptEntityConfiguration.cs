using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesTrainer.Api.Features.Exercises.Models;

namespace SalesTrainer.Api.Infrastructure.Data;

public class ExerciseTypePromptEntityConfiguration : IEntityTypeConfiguration<ExerciseTypePrompt>
{
    public void Configure(EntityTypeBuilder<ExerciseTypePrompt> builder)
    {
        builder.HasKey(p => p.Id);

        builder.HasIndex(p => p.ExerciseType)
            .IsUnique();

        builder.Property(p => p.ExerciseType)
            .IsRequired();

        builder.Property(p => p.SystemPrompt)
            .IsRequired();
    }
}
