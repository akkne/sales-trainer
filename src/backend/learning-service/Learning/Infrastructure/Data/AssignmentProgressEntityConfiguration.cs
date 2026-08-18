using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Assignments.Models;

namespace Sellevate.Learning.Infrastructure.Data;

public sealed class AssignmentProgressEntityConfiguration : IEntityTypeConfiguration<AssignmentProgress>
{
    public void Configure(EntityTypeBuilder<AssignmentProgress> builder)
    {
        builder.ToTable("AssignmentProgressRecords");

        builder.HasKey(record => record.Id);

        builder.Property(record => record.OrganizationId).IsRequired();
        builder.Property(record => record.AssignmentId).IsRequired();
        builder.Property(record => record.UserId).IsRequired();

        builder.Property(record => record.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue(AssignmentProgressStatuses.NotStarted);

        builder.Property(record => record.BestScore);
        builder.Property(record => record.AttemptCount).IsRequired();
        builder.Property(record => record.FirstOpenedAt);
        builder.Property(record => record.CompletedAt);

        // One row per person per assignment. The same person may belong to two organizations
        // (memberships, 40.6) and then holds one row per organization's assignment, which the leading
        // OrganizationId already separates.
        builder.HasIndex(record => new { record.OrganizationId, record.AssignmentId, record.UserId })
            .IsUnique();

        // "Which assignments is this person still on" — 40.23's manager screen, inside one organization.
        builder.HasIndex(record => new { record.OrganizationId, record.UserId, record.Status });

        // Deliberately NOT tenant-leading, unlike every other index here. It serves two things that
        // both need AssignmentId first: the funnel of one assignment (40.25), and the ON DELETE
        // RESTRICT check on the foreign key. Without a leading-AssignmentId index Postgres scans this
        // whole table on every attempt to delete an assignment — the same trap 40.12 documented when
        // its child indexes stopped covering their foreign key.
        builder.HasIndex(record => new { record.AssignmentId, record.Status });
    }
}
