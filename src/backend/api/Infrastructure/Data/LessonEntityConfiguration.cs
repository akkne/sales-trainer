using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesTrainer.Api.Features.Lessons.Models;

namespace SalesTrainer.Api.Infrastructure.Data;

public class LessonEntityConfiguration : IEntityTypeConfiguration<Lesson>
{
    public void Configure(EntityTypeBuilder<Lesson> builder)
    {
        builder.ToTable("Lessons");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Title).IsRequired();

        builder.HasOne(l => l.Topic)
            .WithMany()
            .HasForeignKey(l => l.TopicId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(l => new { l.TopicId, l.OrderInTopic });
    }
}
