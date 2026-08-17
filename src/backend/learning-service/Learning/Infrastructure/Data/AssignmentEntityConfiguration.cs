using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Assignments.Models;

namespace Sellevate.Learning.Infrastructure.Data;

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

        builder.HasMany(assignment => assignment.ProgressRecords)
            .WithOne(record => record.Assignment)
            .HasForeignKey(record => record.AssignmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Tenant-leading, per the convention every 40.10-40.17 table follows: the query filter and the
        // RLS policy put OrganizationId in front of every predicate. Status and Deadline follow it
        // because every screen this table has — the РОП's list, 40.26's "who is due tomorrow" — asks
        // for one status inside one organization, ordered by when it is due.
        builder.HasIndex(assignment => new { assignment.OrganizationId, assignment.Status, assignment.Deadline });

        builder.HasIndex(assignment => new { assignment.OrganizationId, assignment.CreatedAt });
    }
}
