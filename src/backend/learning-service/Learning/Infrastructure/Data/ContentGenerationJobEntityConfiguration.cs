using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.ContentGeneration.Models;

namespace Sellevate.Learning.Infrastructure.Data;

public sealed class ContentGenerationJobEntityConfiguration : IEntityTypeConfiguration<ContentGenerationJob>
{
    public void Configure(EntityTypeBuilder<ContentGenerationJob> builder)
    {
        builder.ToTable("ContentGenerationJobs");

        builder.HasKey(job => job.Id);

        builder.Property(job => job.OrganizationId).IsRequired();

        builder.Property(job => job.Title).IsRequired().HasMaxLength(200);

        // Unbounded text, like Exercise.SerializedContent and the profile's prose columns. The
        // ceiling that matters is the one the service enforces before the row is written (60 000
        // characters), because it is the same number the prompt has to survive; a column width would
        // turn a paste that is slightly too long into a 500 instead of a message.
        builder.Property(job => job.SourceMaterial).IsRequired();

        builder.Property(job => job.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue(ContentGenerationJobStatuses.Structuring);

        builder.Property(job => job.Structure).HasColumnType("jsonb");

        builder.Property(job => job.FailureReason).HasMaxLength(1000);

        // The worker's own query: which runs of this organization are waiting for a call. Status
        // first because every worker query names one.
        builder.HasIndex(job => new { job.OrganizationId, job.Status, job.CreatedAt });

        // The administrator's list, newest first.
        builder.HasIndex(job => new { job.OrganizationId, job.CreatedAt });

        // The produced lesson, from the other side: "where did this lesson come from" is the question
        // asked of a generated lesson somebody is unsure about, and 40.31 will ask it by lesson id.
        builder.HasIndex(job => new { job.OrganizationId, job.ProducedLessonId });
    }
}
