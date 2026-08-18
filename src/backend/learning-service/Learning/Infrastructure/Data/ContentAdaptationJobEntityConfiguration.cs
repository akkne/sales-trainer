using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.ContentAdaptation.Models;

namespace Sellevate.Learning.Infrastructure.Data;

public sealed class ContentAdaptationJobEntityConfiguration : IEntityTypeConfiguration<ContentAdaptationJob>
{
    public void Configure(EntityTypeBuilder<ContentAdaptationJob> builder)
    {
        builder.ToTable("ContentAdaptationJobs");

        builder.HasKey(job => job.Id);

        builder.Property(job => job.OrganizationId).IsRequired();

        builder.Property(job => job.Mode)
            .IsRequired()
            .HasMaxLength(32)
            .HasDefaultValue(ContentAdaptationModes.ToneRewrite);

        // The same width TeamSkillGapDismissals.StageKey has, and for the same reason: both hold a
        // Skill.Stage value and a mismatch would let one table accept a key the other truncates.
        builder.Property(job => job.StageKey).IsRequired().HasMaxLength(64);

        builder.Property(job => job.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue(ContentAdaptationStatuses.Preparing);

        builder.Property(job => job.FailureReason).HasMaxLength(1000);

        builder.HasMany(job => job.Items)
            .WithOne(item => item.Job)
            .HasForeignKey(item => item.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        // The worker's own query: which batches of this organization still owe somebody a call.
        builder.HasIndex(job => new { job.OrganizationId, job.Status, job.CreatedAt });

        // The administrator's list, newest first.
        builder.HasIndex(job => new { job.OrganizationId, job.CreatedAt });

        // «Не запускай второй такой же прогон». One live batch per stage per mode, enforced by the
        // database rather than by a read-then-insert — under READ COMMITTED two clicks a second apart
        // would both see no live batch and both start one, and the customer would pay twice for sixty
        // rewrites of the same sixty exercises. Partial, because a finished batch must not block the
        // next one: the whole point of the queue is that it eventually empties.
        builder.HasIndex(job => new { job.OrganizationId, job.Mode, job.StageKey })
            .IsUnique()
            .HasDatabaseName("UX_ContentAdaptationJobs_Live")
            .HasFilter("\"Status\" IN ('preparing', 'awaiting_review')");
    }
}
