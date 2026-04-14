using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesTrainer.Api.Features.Lessons.Models;

namespace SalesTrainer.Api.Infrastructure.Data;

public class ExerciseEntityConfiguration : IEntityTypeConfiguration<Exercise>
{
    public void Configure(EntityTypeBuilder<Exercise> builder)
    {
        builder.ToTable("Exercises");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Type).IsRequired();
        builder.Property(e => e.SerializedContent).HasColumnType("jsonb");

        builder.HasOne(e => e.Lesson)
            .WithMany()
            .HasForeignKey(e => e.LessonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.LessonId, e.OrderInLesson });
    }
}
