using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Assignments.Models;

namespace Sellevate.Learning.Infrastructure.Data;

/// <summary>
/// Maps one person's standing on one assignment.
///
/// <para>
/// <b>One row per person per assignment.</b> The same person may belong to two organizations
/// (memberships, 40.6) and then holds one row per organization's assignment, which the leading
/// organization column already separates.
/// </para>
///
/// <para>
/// The tenant-leading index answers "which assignments is this person still on" — 40.23's manager
/// screen, inside one organization.
/// </para>
///
/// <para>
/// The index on (assignment, status) is deliberately <b>not</b> tenant-leading, unlike every other index
/// here. It serves two things that both need the assignment first: the funnel of one assignment (40.25),
/// and the <c>ON DELETE RESTRICT</c> check on the foreign key. Without a leading-assignment index
/// Postgres scans this whole table on every attempt to delete an assignment — the same trap 40.12
/// documented when its child indexes stopped covering their foreign key.
/// </para>
/// </summary>
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

        builder.HasIndex(record => new { record.OrganizationId, record.AssignmentId, record.UserId })
            .IsUnique();

        builder.HasIndex(record => new { record.OrganizationId, record.UserId, record.Status });

        builder.HasIndex(record => new { record.AssignmentId, record.Status });
    }
}
