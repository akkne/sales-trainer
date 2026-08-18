using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Ai.Features.Quotas.Models;

namespace Sellevate.Ai.Infrastructure.Data;

public sealed class OrganizationQuotaEntityConfiguration : IEntityTypeConfiguration<OrganizationQuota>
{
    public void Configure(EntityTypeBuilder<OrganizationQuota> builder)
    {
        builder.ToTable("OrganizationQuotas");

        builder.HasKey(quota => quota.OrganizationId);

        builder.Property(quota => quota.OrganizationId).ValueGeneratedNever();

        builder.Property(quota => quota.Note).HasMaxLength(1000);

        builder.Property(quota => quota.UpdatedAt).IsRequired();
    }
}

public sealed class AiUsageRecordEntityConfiguration : IEntityTypeConfiguration<AiUsageRecord>
{
    public void Configure(EntityTypeBuilder<AiUsageRecord> builder)
    {
        builder.ToTable("AiUsageRecords");

        // The tenant column leads the key, so every read the meter makes is a prefix scan of one
        // organization's dozen-or-so rows for the month and never a filter over the table.
        builder.HasKey(record => new { record.OrganizationId, record.PeriodKey, record.Model });

        builder.Property(record => record.OrganizationId).ValueGeneratedNever();

        builder.Property(record => record.PeriodKey).IsRequired().HasMaxLength(7);

        builder.Property(record => record.Model).IsRequired().HasMaxLength(128);

        builder.Property(record => record.Kind).IsRequired().HasMaxLength(16);

        builder.Property(record => record.UpdatedAt).IsRequired();
    }
}
