using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Features.Exercises.Models;

namespace Sellevate.Learning.Infrastructure.Data;

public sealed class ExerciseTypePromptEntityConfiguration : IEntityTypeConfiguration<ExerciseTypePrompt>
{
    public void Configure(EntityTypeBuilder<ExerciseTypePrompt> builder)
    {
        builder.HasKey(prompt => prompt.Id);

        builder.HasIndex(prompt => prompt.ExerciseType)
            .IsUnique();

        builder.Property(prompt => prompt.ExerciseType)
            .IsRequired();

        builder.Property(prompt => prompt.SystemPrompt)
            .IsRequired();
    }
}
