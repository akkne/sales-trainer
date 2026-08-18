using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Assignments.Models;

namespace Sellevate.Learning.Infrastructure.Data;

/// <summary>
/// Maps the assignment a РОП issues, and shapes its two indexes around the two questions the table is
/// actually asked.
///
/// <para>
/// <b>Tenant-leading</b>, per the convention every 40.10–40.17 table follows: the query filter and the
/// RLS policy put the organization in front of every predicate. Status and deadline follow it because
/// every screen this table has — the РОП's list, 40.26's "who is due tomorrow" — asks for one status
/// inside one organization, ordered by when it is due.
/// </para>
///
/// <para>
/// Phase 40.24. A repeat points at the assignment a human created, with <c>RESTRICT</c> rather than
/// <c>CASCADE</c> for the same reason the progress foreign key is <c>RESTRICT</c>: an origin is the
/// record of what was asked, and deleting it out from under three waves of scores would rewrite history.
/// In practice it never fires — only a draft may be deleted (<c>AssignmentService</c>), and a draft has
/// never been issued, so it can have no waves.
/// </para>
///
/// <para>
/// Phase 40.24. The unique index on (origin, wave) is the <b>idempotency guarantee of the repeat
/// sweep</b>, and the reason the sweep needs no "already issued" flag anywhere: a wave has been issued
/// exactly when its row exists, so two ticks racing inside one window collide here rather than issuing
/// the same shortened work to the same people twice. It is deliberately <b>not</b> tenant-leading — the
/// second such exception in this feature, for the same two reasons 40.21 recorded on
/// <c>IX_AssignmentProgressRecords_AssignmentId_Status</c>: it is the only index covering the foreign
/// key above, so without it Postgres scans the whole table on every attempt to delete an assignment;
/// and an origin id is globally unique, so leading with the organization would weaken the uniqueness
/// rather than scope it.
/// </para>
/// </summary>
public sealed class AssignmentEntityConfiguration : IEntityTypeConfiguration<Assignment>
{
    public void Configure(EntityTypeBuilder<Assignment> builder)
    {
        builder.ToTable("Assignments");

        builder.HasKey(assignment => assignment.Id);

        builder.Property(assignment => assignment.OrganizationId).IsRequired();
        builder.Property(assignment => assignment.CreatedBy);

        builder.Property(assignment => assignment.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(assignment => assignment.Goal)
            .HasMaxLength(2000);

        builder.Property(assignment => assignment.SourceType)
            .IsRequired()
            .HasMaxLength(16);

        builder.Property(assignment => assignment.SourceRef)
            .HasMaxLength(200);

        builder.Property(assignment => assignment.Content)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(assignment => assignment.Audience)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(assignment => assignment.CompletionRule)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(assignment => assignment.RepeatSchedule)
            .HasColumnType("jsonb");

        builder.Property(assignment => assignment.RepeatOfAssignmentId);
        builder.Property(assignment => assignment.RepeatWaveIndex);

        builder.Property(assignment => assignment.OpensAt);
        builder.Property(assignment => assignment.Deadline);

        builder.Property(assignment => assignment.Status)
            .IsRequired()
            .HasMaxLength(16)
            .HasDefaultValue(AssignmentStatuses.Draft);

        builder.Property(assignment => assignment.CreatedAt).IsRequired();
        builder.Property(assignment => assignment.UpdatedAt).IsRequired();
        builder.Property(assignment => assignment.ActivatedAt);
        builder.Property(assignment => assignment.ClosedAt);
        builder.Property(assignment => assignment.DeadlineNoticeSentAt);

        builder.HasMany(assignment => assignment.ProgressRecords)
            .WithOne(record => record.Assignment)
            .HasForeignKey(record => record.AssignmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(assignment => new { assignment.OrganizationId, assignment.Status, assignment.Deadline });

        builder.HasIndex(assignment => new { assignment.OrganizationId, assignment.CreatedAt });

        builder.HasOne<Assignment>()
            .WithMany()
            .HasForeignKey(assignment => assignment.RepeatOfAssignmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(assignment => new { assignment.RepeatOfAssignmentId, assignment.RepeatWaveIndex })
            .IsUnique()
            .HasFilter("\"RepeatOfAssignmentId\" IS NOT NULL");
    }
}
