using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Company.Common.Constants;
using Sellevate.Company.Features.Companies.Models;

namespace Sellevate.Company.Features.Companies.Configurations;

/// <summary>
/// Maps the <c>CallLogEntries</c> table — the timeline of real calls logged against a company.
///
/// <para>
/// The timeline index leads with <c>OrganizationId</c> and descends on <c>OccurredAt</c> to match
/// the only read that exists: newest-first within one company within one organization. It replaces
/// <c>IX_CallLogEntries_CompanyId_OccurredAt</c>, which
/// <c>docs/TENANCY/sql/40.12_company_organization_indexes_concurrently.sql</c> drops once this one
/// is valid rather than a migration dropping it under a lock.
/// </para>
/// </summary>
internal sealed class CallLogEntryEntityConfiguration : IEntityTypeConfiguration<CallLogEntry>
{
    public void Configure(EntityTypeBuilder<CallLogEntry> builder)
    {
        builder.ToTable("CallLogEntries");

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.OrganizationId)
            .IsRequired();

        builder.Property(entry => entry.ContactName)
            .IsRequired()
            .HasMaxLength(CompanyFieldLengths.Name);

        builder.Property(entry => entry.Subject)
            .IsRequired()
            .HasMaxLength(CompanyFieldLengths.CallLogSubject);

        builder.Property(entry => entry.Outcome)
            .IsRequired()
            .HasMaxLength(CompanyFieldLengths.CallLogOutcome);

        builder.Property(entry => entry.OccurredAt)
            .IsRequired();

        builder.Property(entry => entry.CreatedAt)
            .IsRequired();

        builder.Property(entry => entry.UpdatedAt)
            .IsRequired();

        builder.HasIndex(entry => new { entry.OrganizationId, entry.CompanyId, entry.OccurredAt })
            .HasDatabaseName("IX_CallLogEntries_OrganizationId_CompanyId_OccurredAt")
            .IsDescending(false, false, true);

        builder.HasIndex(entry => new { entry.OrganizationId, entry.UserId })
            .HasDatabaseName("IX_CallLogEntries_OrganizationId_UserId");
    }
}
