using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Features.Exercises.Models;

namespace Sellevate.Learning.Infrastructure.Data;

/// <summary>
/// Maps the per-exercise-type system prompt. Platform-wide with no organization column, and unique on the
/// exercise type: the type is what a caller looks the prompt up by, so a second row would make the lookup
/// ambiguous.
/// </summary>
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
