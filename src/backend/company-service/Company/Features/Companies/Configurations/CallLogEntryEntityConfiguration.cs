using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Company.Features.Companies.Models;

namespace Sellevate.Company.Features.Companies.Configurations;

public sealed class CallLogEntryEntityConfiguration : IEntityTypeConfiguration<CallLogEntry>
{
    public void Configure(EntityTypeBuilder<CallLogEntry> builder)
    {
        builder.ToTable("CallLogEntries");

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.OrganizationId)
            .IsRequired();

        builder.Property(entry => entry.ContactName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(entry => entry.Subject)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(entry => entry.Outcome)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(entry => entry.OccurredAt)
            .IsRequired();

        builder.Property(entry => entry.CreatedAt)
            .IsRequired();

        builder.Property(entry => entry.UpdatedAt)
            .IsRequired();

        // Phase 40.12: the timeline read is always scoped to one organization first, so the
        // organization leads the index. Replaces IX_CallLogEntries_CompanyId_OccurredAt, which the
        // concurrent-index script drops once this one is valid.
        builder.HasIndex(entry => new { entry.OrganizationId, entry.CompanyId, entry.OccurredAt })
            .HasDatabaseName("IX_CallLogEntries_OrganizationId_CompanyId_OccurredAt")
            .IsDescending(false, false, true);

        builder.HasIndex(entry => new { entry.OrganizationId, entry.UserId })
            .HasDatabaseName("IX_CallLogEntries_OrganizationId_UserId");
    }
}
