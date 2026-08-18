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
        builder.Property(lesson => lesson.OrganizationId);
        builder.Property(lesson => lesson.Title).IsRequired();

        builder.Property(lesson => lesson.Slug)
            .IsRequired()
            .HasMaxLength(160);

        builder.Property(lesson => lesson.IsArchived)
            .IsRequired()
            .HasDefaultValue(false);

        builder.HasOne(lesson => lesson.Topic)
            .WithMany()
            .HasForeignKey(lesson => lesson.TopicId)
            .OnDelete(DeleteBehavior.Cascade);

        // Phase 40.15. Restrict, not cascade: a global lesson that three customers have overridden
        // must not be deletable in one click, and silently promoting the overrides to standalone
        // lessons (SetNull) would lose the fact that they were ever derived. Nothing creates an
        // override until 40.18, so today this constrains nothing.
        builder.HasOne(lesson => lesson.ParentLesson)
            .WithMany()
            .HasForeignKey(lesson => lesson.ParentLessonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(lesson => new { lesson.OrganizationId, lesson.TopicId, lesson.OrderInTopic });

        // Phase 40.15, the trap already paid for once in 40.10: in a composite unique index Postgres
        // treats NULLs as distinct, so ("OrganizationId", "Slug") does NOT stop two global lessons
        // sharing a slug. The partial index over the global rows is what preserves that guarantee,
        // exactly as for Skill.IconicName / Topic.IconicName / Technique.Slug.
        builder.HasIndex(lesson => new { lesson.OrganizationId, lesson.Slug })
            .IsUnique();
        builder.HasIndex(lesson => lesson.Slug)
            .IsUnique()
            .HasFilter("\"OrganizationId\" IS NULL")
            .HasDatabaseName("IX_Lessons_Slug_Global");

        builder.HasIndex(lesson => lesson.ParentLessonId);
    }
}
