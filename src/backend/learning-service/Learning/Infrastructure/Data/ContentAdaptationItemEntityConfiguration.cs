using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.ContentAdaptation.Models;

namespace Sellevate.Learning.Infrastructure.Data;

public sealed class ContentAdaptationItemEntityConfiguration : IEntityTypeConfiguration<ContentAdaptationItem>
{
    public void Configure(EntityTypeBuilder<ContentAdaptationItem> builder)
    {
        builder.ToTable("ContentAdaptationItems");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.OrganizationId).IsRequired();

        builder.Property(item => item.LessonTitle).IsRequired().HasMaxLength(300);

        builder.Property(item => item.ExerciseType).IsRequired().HasMaxLength(50);

        // Lowercase hex SHA-256, the width LessonVersion.ContentHash uses.
        builder.Property(item => item.BaseContentHash).IsRequired().HasMaxLength(64);

        builder.Property(item => item.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue(ContentAdaptationItemStatuses.Pending);

        // jsonb, like Exercise.SerializedContent — this column holds a body that may become one.
        builder.Property(item => item.ProposedContent).HasColumnType("jsonb");

        // jsonb, like ContentGenerationJobs.Insufficiency and for the same reason: it is a list of
        // codes with a shape, and the flat alternative would put the code and its sentence in two
        // places that can disagree.
        builder.Property(item => item.Findings).HasColumnType("jsonb");

        builder.Property(item => item.ChangeSummary).HasMaxLength(500);

        builder.Property(item => item.FailureReason).HasMaxLength(1000);

        // The worker's query: which items of a claimed batch still owe a call, oldest first.
        builder.HasIndex(item => new { item.OrganizationId, item.JobId, item.Status });

        // The queue as the screen walks it — the learner's order, so a reviewer reads a lesson's
        // exercises in the order the lesson plays them.
        builder.HasIndex(item => new { item.OrganizationId, item.JobId, item.LessonId, item.OrderInLesson });

        // «Есть ли на это упражнение живое предложение» — asked when a second batch would otherwise
        // propose a rewrite of an exercise somebody has not answered about yet.
        builder.HasIndex(item => new { item.OrganizationId, item.ExerciseId, item.Status });
    }
}
