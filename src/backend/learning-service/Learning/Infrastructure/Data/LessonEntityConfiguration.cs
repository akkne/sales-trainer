using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Features.Lessons.Models;

namespace Sellevate.Learning.Infrastructure.Data;

/// <summary>
/// Maps a lesson, which may be a global library row or one organization's override of one.
///
/// <para>
/// Phase 40.15. The base-lesson foreign key is <c>Restrict</c>, not <c>Cascade</c>: a global lesson that
/// three customers have overridden must not be deletable in one click, and silently promoting the
/// overrides to standalone lessons (<c>SetNull</c>) would lose the fact that they were ever derived.
/// Nothing creates an override until 40.18, so today this constrains nothing.
/// </para>
///
/// <para>
/// Phase 40.15, the trap already paid for once in 40.10: <b>in a composite unique index Postgres treats
/// nulls as distinct</b>, so (organization, slug) does <b>not</b> stop two global lessons sharing a slug.
/// The partial unique index over the global rows is what preserves that guarantee, exactly as for
/// <c>Skill.IconicName</c>, <c>Topic.IconicName</c> and <c>Technique.Slug</c>.
/// </para>
/// </summary>
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

        builder.HasOne(lesson => lesson.ParentLesson)
            .WithMany()
            .HasForeignKey(lesson => lesson.ParentLessonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(lesson => new { lesson.OrganizationId, lesson.TopicId, lesson.OrderInTopic });

        builder.HasIndex(lesson => new { lesson.OrganizationId, lesson.Slug })
            .IsUnique();
        builder.HasIndex(lesson => lesson.Slug)
            .IsUnique()
            .HasFilter("\"OrganizationId\" IS NULL")
            .HasDatabaseName("IX_Lessons_Slug_Global");

        builder.HasIndex(lesson => lesson.ParentLessonId);
    }
}
