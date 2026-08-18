using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Company.Common.Constants;
using Sellevate.Company.Features.Companies.Models;
using CompanyEntity = Sellevate.Company.Features.Companies.Models.Company;

namespace Sellevate.Company.Features.Companies.Configurations;

/// <summary>
/// Maps the <c>Companies</c> table: the CRM root every other entity here hangs off.
///
/// <para>
/// Both indexes lead with <c>OrganizationId</c> because from Phase 40.12 on every query in this
/// service filters by organization first — the query filter adds it even where the call site does
/// not. <c>IX_Companies_OrganizationId_UserId</c> mirrors the double scope, which is the access path
/// of every read, and is a strict superset of the old <c>IX_Companies_UserId</c>; the follow-up
/// index is filtered to rows that actually have a scheduled follow-up so the reminder poll's
/// <c>NextActionAt &lt;= now AND FollowUpNotifiedAt IS NULL</c> scan stays proportional to the work
/// pending rather than to the table.
/// </para>
///
/// <para>
/// The superseded single-column indexes are dropped by
/// <c>docs/TENANCY/sql/40.12_company_organization_indexes_concurrently.sql</c> once these are built,
/// not by a migration, so building them never locks a live table.
/// </para>
/// </summary>
internal sealed class CompanyEntityConfiguration : IEntityTypeConfiguration<CompanyEntity>
{
    public void Configure(EntityTypeBuilder<CompanyEntity> builder)
    {
        builder.ToTable("Companies");

        builder.HasKey(company => company.Id);

        builder.Property(company => company.OrganizationId)
            .IsRequired();

        builder.Property(company => company.Name)
            .IsRequired()
            .HasMaxLength(CompanyFieldLengths.Name);

        builder.Property(company => company.Description)
            .IsRequired()
            .HasMaxLength(CompanyFieldLengths.CompanyDescription)
            .HasDefaultValue(string.Empty);

        builder.Property(company => company.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(CompanyFieldLengths.CompanyStatusColumn)
            .HasDefaultValue(CompanyStatus.Lead);

        builder.Property(company => company.NextActionAt)
            .IsRequired(false);

        builder.Property(company => company.NextActionNote)
            .IsRequired(false)
            .HasMaxLength(CompanyFieldLengths.NextActionNote);

        builder.Property(company => company.FollowUpNotifiedAt)
            .IsRequired(false);

        builder.Property(company => company.BriefingContent)
            .IsRequired(false);

        builder.Property(company => company.BriefingGeneratedAt)
            .IsRequired(false);

        builder.Property(company => company.ReadinessJson)
            .IsRequired(false);

        builder.Property(company => company.ReadinessGeneratedAt)
            .IsRequired(false);

        builder.Property(company => company.ReadinessNoFeedbackUntil)
            .IsRequired(false);

        builder.Property(company => company.CreatedAt)
            .IsRequired();

        builder.Property(company => company.UpdatedAt)
            .IsRequired();

        builder.HasIndex(company => new { company.OrganizationId, company.UserId })
            .HasDatabaseName("IX_Companies_OrganizationId_UserId");

        builder.HasIndex(company => new { company.OrganizationId, company.NextActionAt })
            .HasDatabaseName("IX_Companies_OrganizationId_NextActionAt")
            .HasFilter("\"NextActionAt\" IS NOT NULL");

        builder.HasMany(company => company.CallLogEntries)
            .WithOne(entry => entry.Company)
            .HasForeignKey(entry => entry.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(company => company.PracticeCalls)
            .WithOne(practiceCall => practiceCall.Company)
            .HasForeignKey(practiceCall => practiceCall.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(company => company.Contacts)
            .WithOne(contact => contact.Company)
            .HasForeignKey(contact => contact.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(company => company.Personas)
            .WithOne(persona => persona.Company)
            .HasForeignKey(persona => persona.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
