using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Company.Features.Companies.Models;

namespace Sellevate.Company.Features.Companies.Configurations;

public sealed class PracticeCallEntityConfiguration : IEntityTypeConfiguration<PracticeCall>
{
    public void Configure(EntityTypeBuilder<PracticeCall> builder)
    {
        builder.ToTable("PracticeCalls");

        builder.HasKey(practiceCall => practiceCall.Id);

        builder.Property(practiceCall => practiceCall.OrganizationId)
            .IsRequired();

        builder.Property(practiceCall => practiceCall.DialogSessionId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(practiceCall => practiceCall.Goal)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(practiceCall => practiceCall.CreatedAt)
            .IsRequired();

        builder.HasIndex(practiceCall => new { practiceCall.OrganizationId, practiceCall.CompanyId, practiceCall.CreatedAt })
            .HasDatabaseName("IX_PracticeCalls_OrganizationId_CompanyId_CreatedAt")
            .IsDescending(false, false, true);

        builder.HasIndex(practiceCall => new { practiceCall.OrganizationId, practiceCall.UserId })
            .HasDatabaseName("IX_PracticeCalls_OrganizationId_UserId");
    }
}
