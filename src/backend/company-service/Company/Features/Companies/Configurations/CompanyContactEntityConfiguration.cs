using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Company.Common.Constants;
using Sellevate.Company.Features.Companies.Models;

namespace Sellevate.Company.Features.Companies.Configurations;

/// <summary>
/// Maps the <c>CompanyContacts</c> table — the real people at a company, as opposed to the
/// practice personas modelled on them.
///
/// <para>
/// A contact is deleted with <c>SetNull</c> on its call-log entries rather than cascading: the
/// history of a call that happened is not invalidated by the person leaving, and the entry keeps its
/// denormalised <c>ContactName</c> so the timeline still reads correctly afterwards.
/// </para>
/// </summary>
internal sealed class CompanyContactEntityConfiguration : IEntityTypeConfiguration<CompanyContact>
{
    public void Configure(EntityTypeBuilder<CompanyContact> builder)
    {
        builder.ToTable("CompanyContacts");

        builder.HasKey(contact => contact.Id);

        builder.Property(contact => contact.OrganizationId)
            .IsRequired();

        builder.Property(contact => contact.Name)
            .IsRequired()
            .HasMaxLength(CompanyFieldLengths.Name);

        builder.Property(contact => contact.Position)
            .IsRequired()
            .HasMaxLength(CompanyFieldLengths.Position)
            .HasDefaultValue(string.Empty);

        builder.Property(contact => contact.Notes)
            .IsRequired()
            .HasMaxLength(CompanyFieldLengths.ContactNotes)
            .HasDefaultValue(string.Empty);

        builder.Property(contact => contact.CreatedAt)
            .IsRequired();

        builder.Property(contact => contact.UpdatedAt)
            .IsRequired();

        builder.HasIndex(contact => new { contact.OrganizationId, contact.CompanyId, contact.CreatedAt })
            .HasDatabaseName("IX_CompanyContacts_OrganizationId_CompanyId_CreatedAt")
            .IsDescending(false, false, true);

        builder.HasIndex(contact => new { contact.OrganizationId, contact.UserId })
            .HasDatabaseName("IX_CompanyContacts_OrganizationId_UserId");

        builder.HasMany(contact => contact.CallLogEntries)
            .WithOne(entry => entry.Contact)
            .HasForeignKey(entry => entry.ContactId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
