using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Company.Features.Companies.Models;
using CompanyEntity = Sellevate.Company.Features.Companies.Models.Company;

namespace Sellevate.Company.Features.Companies.Configurations;

public sealed class CompanyEntityConfiguration : IEntityTypeConfiguration<CompanyEntity>
{
    public void Configure(EntityTypeBuilder<CompanyEntity> builder)
    {
        builder.ToTable("Companies");

        builder.HasKey(company => company.Id);

        builder.Property(company => company.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(company => company.Description)
            .IsRequired()
            .HasMaxLength(8000)
            .HasDefaultValue(string.Empty);

        builder.Property(company => company.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(CompanyStatus.Lead);

        builder.Property(company => company.NextActionAt)
            .IsRequired(false);

        builder.Property(company => company.NextActionNote)
            .IsRequired(false)
            .HasMaxLength(2000);

        builder.Property(company => company.FollowUpNotifiedAt)
            .IsRequired(false);

        builder.Property(company => company.BriefingContent)
            .IsRequired(false);

        builder.Property(company => company.BriefingGeneratedAt)
            .IsRequired(false);

        builder.Property(company => company.CreatedAt)
            .IsRequired();

        builder.Property(company => company.UpdatedAt)
            .IsRequired();

        builder.HasIndex(company => company.UserId);

        // Sparse index (only rows with a scheduled follow-up) so the reminder poll's
        // WHERE NextActionAt <= now AND FollowUpNotifiedAt IS NULL stays cheap as the table grows.
        builder.HasIndex(company => company.NextActionAt)
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
    }
}
