using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.ContentGeneration.Models;

namespace Sellevate.Learning.Infrastructure.Data;

/// <summary>
/// Maps one run of the admin content pipeline, and the four questions the table is asked.
///
/// <para>
/// The source material is <b>unbounded text</b>, like <c>Exercise.SerializedContent</c> and the
/// profile's prose columns. The ceiling that matters is the one the service enforces before the row is
/// written (60 000 characters), because it is the same number the prompt has to survive; a column width
/// would turn a paste that is slightly too long into a 500 instead of a message.
/// </para>
///
/// <para>
/// Phase 40.28. The recorded refusal — a list of gaps with the sentence each one shows — is <c>jsonb</c>
/// for the same reason the structure is: it is a document with a shape, and the flat alternative (a
/// comma-separated code column plus a prose column) would put the sentence and the code in two places
/// that can disagree. Phase 40.31's gap reference is the same width <c>Assignments.SourceRef</c> has,
/// because the value is copied straight into it when a gap-detected assignment is created from this run.
/// </para>
///
/// <para>
/// The indexes answer, in order: the worker's own query — which runs of this organization are waiting
/// for a call, status first because every worker query names one; the administrator's list, newest
/// first; "where did this lesson come from", asked of a generated lesson somebody is unsure about; and
/// (40.31) "is a run already working on this gap", asked once per red cell every time the suggestion
/// panel is drawn. The last is partial, because the overwhelming majority of runs were started by a
/// person pasting material and have nothing to say about a gap.
/// </para>
/// </summary>
public sealed class ContentGenerationJobEntityConfiguration : IEntityTypeConfiguration<ContentGenerationJob>
{
    public void Configure(EntityTypeBuilder<ContentGenerationJob> builder)
    {
        builder.ToTable("ContentGenerationJobs");

        builder.HasKey(job => job.Id);

        builder.Property(job => job.OrganizationId).IsRequired();

        builder.Property(job => job.Title).IsRequired().HasMaxLength(200);

        builder.Property(job => job.SourceMaterial).IsRequired();

        builder.Property(job => job.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue(ContentGenerationJobStatuses.Structuring);

        builder.Property(job => job.Structure).HasColumnType("jsonb");

        builder.Property(job => job.Insufficiency).HasColumnType("jsonb");

        builder.Property(job => job.FailureReason).HasMaxLength(1000);

        builder.Property(job => job.GapSourceRef).HasMaxLength(200);

        builder.HasIndex(job => new { job.OrganizationId, job.Status, job.CreatedAt });

        builder.HasIndex(job => new { job.OrganizationId, job.CreatedAt });

        builder.HasIndex(job => new { job.OrganizationId, job.ProducedLessonId });

        builder.HasIndex(job => new { job.OrganizationId, job.GapSourceRef })
            .HasFilter("\"GapSourceRef\" IS NOT NULL");
    }
}
