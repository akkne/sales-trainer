using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Features.Lessons.Models;

namespace Sellevate.Learning.Infrastructure.Data;

public sealed class LessonEntityConfiguration : IEntityTypeConfiguration<Lesson>
{
    public void Configure(EntityTypeBuilder<Lesson> builder)
    {
        builder.ToTable("Lessons");
        builder.HasKey(lesson => lesson.Id);
        builder.Property(lesson => lesson.Title).IsRequired();

        builder.HasOne(lesson => lesson.Topic)
            .WithMany()
            .HasForeignKey(lesson => lesson.TopicId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(lesson => new { lesson.TopicId, lesson.OrderInTopic });
    }
}
