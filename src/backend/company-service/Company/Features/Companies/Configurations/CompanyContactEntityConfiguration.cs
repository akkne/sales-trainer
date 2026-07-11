using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Company.Features.Companies.Models;

namespace Sellevate.Company.Features.Companies.Configurations;

public sealed class CompanyContactEntityConfiguration : IEntityTypeConfiguration<CompanyContact>
{
    public void Configure(EntityTypeBuilder<CompanyContact> builder)
    {
        builder.ToTable("CompanyContacts");

        builder.HasKey(contact => contact.Id);

        builder.Property(contact => contact.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(contact => contact.Position)
            .IsRequired()
            .HasMaxLength(200)
            .HasDefaultValue(string.Empty);

        builder.Property(contact => contact.Notes)
            .IsRequired()
            .HasMaxLength(2000)
            .HasDefaultValue(string.Empty);

        builder.Property(contact => contact.CreatedAt)
            .IsRequired();

        builder.Property(contact => contact.UpdatedAt)
            .IsRequired();

        builder.HasIndex(contact => new { contact.CompanyId, contact.CreatedAt })
            .HasDatabaseName("IX_CompanyContacts_CompanyId_CreatedAt")
            .IsDescending(false, true);

        builder.HasMany(contact => contact.CallLogEntries)
            .WithOne(entry => entry.Contact)
            .HasForeignKey(entry => entry.ContactId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
