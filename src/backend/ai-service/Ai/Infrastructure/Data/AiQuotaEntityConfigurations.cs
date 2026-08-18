using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Ai.Features.Quotas.Constants;
using Sellevate.Ai.Features.Quotas.Models;

namespace Sellevate.Ai.Infrastructure.Data;

/// <summary>
/// Maps one organization's allowance. The tenant column is the primary key: there is no global quota
/// row, so a second row for the same organization is a bug the database refuses rather than a state the
/// resolver has to pick a winner from.
/// </summary>
public sealed class OrganizationQuotaEntityConfiguration : IEntityTypeConfiguration<OrganizationQuota>
{
    public void Configure(EntityTypeBuilder<OrganizationQuota> builder)
    {
        builder.ToTable("OrganizationQuotas");

        builder.HasKey(quota => quota.OrganizationId);

        builder.Property(quota => quota.OrganizationId).ValueGeneratedNever();

        builder.Property(quota => quota.Note).HasMaxLength(AiQuotaColumnLengths.QuotaNote);

        builder.Property(quota => quota.UpdatedAt).IsRequired();
    }
}

/// <summary>
/// Maps the spend ledger. The tenant column leads the composite key, so every read the meter makes is a
/// prefix scan of one organization's dozen-or-so rows for the month and never a filter over the table —
/// and the same key is what the <c>ON CONFLICT</c> upsert in <c>AiSpendMeter</c> targets.
/// </summary>
public sealed class AiUsageRecordEntityConfiguration : IEntityTypeConfiguration<AiUsageRecord>
{
    public void Configure(EntityTypeBuilder<AiUsageRecord> builder)
    {
        builder.ToTable("AiUsageRecords");

        builder.HasKey(record => new { record.OrganizationId, record.PeriodKey, record.Model });

        builder.Property(record => record.OrganizationId).ValueGeneratedNever();

        builder.Property(record => record.PeriodKey).IsRequired().HasMaxLength(AiQuotaColumnLengths.PeriodKey);

        builder.Property(record => record.Model).IsRequired().HasMaxLength(AiQuotaColumnLengths.Model);

        builder.Property(record => record.Kind).IsRequired().HasMaxLength(AiQuotaColumnLengths.UsageKind);

        builder.Property(record => record.UpdatedAt).IsRequired();
    }
}
