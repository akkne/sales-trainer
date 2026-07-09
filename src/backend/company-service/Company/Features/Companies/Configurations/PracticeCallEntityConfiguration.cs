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

        builder.Property(practiceCall => practiceCall.DialogSessionId)
            .IsRequired();

        builder.Property(practiceCall => practiceCall.Goal)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(practiceCall => practiceCall.CreatedAt)
            .IsRequired();

        builder.HasIndex(practiceCall => new { practiceCall.CompanyId, practiceCall.CreatedAt })
            .HasDatabaseName("IX_PracticeCalls_CompanyId_CreatedAt")
            .IsDescending(false, true);
    }
}
