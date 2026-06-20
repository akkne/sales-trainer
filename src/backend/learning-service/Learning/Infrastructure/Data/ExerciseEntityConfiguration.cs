using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Features.Lessons.Models;

namespace Sellevate.Learning.Infrastructure.Data;

public sealed class ExerciseEntityConfiguration : IEntityTypeConfiguration<Exercise>
{
    public void Configure(EntityTypeBuilder<Exercise> builder)
    {
        builder.ToTable("Exercises");
        builder.HasKey(exercise => exercise.Id);
        builder.Property(exercise => exercise.Type).IsRequired();
        builder.Property(exercise => exercise.SerializedContent).HasColumnType("jsonb");

        builder.HasOne(exercise => exercise.Lesson)
            .WithMany()
            .HasForeignKey(exercise => exercise.LessonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(exercise => new { exercise.LessonId, exercise.OrderInLesson });
    }
}
