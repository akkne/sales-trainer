using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Features.Lessons.Models;

namespace Sellevate.Learning.Infrastructure.Data;

/// <summary>
/// Maps an exercise. The organization column is nullable because an exercise inherits the visibility of
/// its lesson, which may be global; the index is tenant-leading and then follows the reading order the
/// lesson plays. <c>Cascade</c> on the lesson foreign key is deliberate — an exercise has no meaning
/// without the lesson that contains it.
/// </summary>
public sealed class ExerciseEntityConfiguration : IEntityTypeConfiguration<Exercise>
{
    public void Configure(EntityTypeBuilder<Exercise> builder)
    {
        builder.ToTable("Exercises");
        builder.HasKey(exercise => exercise.Id);
        builder.Property(exercise => exercise.OrganizationId);
        builder.Property(exercise => exercise.Type).IsRequired();
        builder.Property(exercise => exercise.SerializedContent).HasColumnType("jsonb");

        builder.HasOne(exercise => exercise.Lesson)
            .WithMany()
            .HasForeignKey(exercise => exercise.LessonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(exercise => new { exercise.OrganizationId, exercise.LessonId, exercise.OrderInLesson });
    }
}
