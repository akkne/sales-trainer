using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.ContentAdaptation.Models;

namespace Sellevate.Learning.Infrastructure.Data;

/// <summary>
/// Maps one proposed rewrite inside an adaptation batch.
///
/// <para>
/// The content hash is lowercase hex SHA-256, the width <c>LessonVersion.ContentHash</c> uses. The
/// proposed body is <c>jsonb</c> like <c>Exercise.SerializedContent</c>, because this column holds a
/// body that may become one; the findings are <c>jsonb</c> like <c>ContentGenerationJobs.Insufficiency</c>
/// and for the same reason — it is a list of codes with a shape, and the flat alternative would put the
/// code and its sentence in two places that can disagree.
/// </para>
///
/// <para>
/// The indexes answer: the worker's query — which items of a claimed batch still owe a call, oldest
/// first; the queue as the screen walks it, in the learner's order so a reviewer reads a lesson's
/// exercises in the order the lesson plays them; and «есть ли на это упражнение живое предложение»,
/// asked when a second batch would otherwise propose a rewrite of an exercise somebody has not answered
/// about yet.
/// </para>
/// </summary>
public sealed class ContentAdaptationItemEntityConfiguration : IEntityTypeConfiguration<ContentAdaptationItem>
{
    public void Configure(EntityTypeBuilder<ContentAdaptationItem> builder)
    {
        builder.ToTable("ContentAdaptationItems");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.OrganizationId).IsRequired();

        builder.Property(item => item.LessonTitle).IsRequired().HasMaxLength(300);

        builder.Property(item => item.ExerciseType).IsRequired().HasMaxLength(50);

        builder.Property(item => item.BaseContentHash).IsRequired().HasMaxLength(64);

        builder.Property(item => item.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue(ContentAdaptationItemStatuses.Pending);

        builder.Property(item => item.ProposedContent).HasColumnType("jsonb");

        builder.Property(item => item.Findings).HasColumnType("jsonb");

        builder.Property(item => item.ChangeSummary).HasMaxLength(500);

        builder.Property(item => item.FailureReason).HasMaxLength(1000);

        builder.HasIndex(item => new { item.OrganizationId, item.JobId, item.Status });

        builder.HasIndex(item => new { item.OrganizationId, item.JobId, item.LessonId, item.OrderInLesson });

        builder.HasIndex(item => new { item.OrganizationId, item.ExerciseId, item.Status });
    }
}
