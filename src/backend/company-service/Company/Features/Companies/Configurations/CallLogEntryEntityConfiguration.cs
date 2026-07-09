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

        builder.HasIndex(entry => new { entry.CompanyId, entry.OccurredAt })
            .HasDatabaseName("IX_CallLogEntries_CompanyId_OccurredAt")
            .IsDescending(false, true);
    }
}
