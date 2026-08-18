namespace Sellevate.Ai.Features.Quotas.Constants;

/// <summary>
/// Widths of the quota feature's text columns, shared by the EF configuration that declares them and
/// by the meter that has to fit a provider-supplied value into one. A value written wider than the
/// column is a failed charge, so the truncation and the schema must agree by construction.
/// </summary>
public static class AiQuotaColumnLengths
{
    /// <summary><c>AiUsageRecords."PeriodKey"</c> — exactly <c>yyyy-MM</c>.</summary>
    public const int PeriodKey = 7;

    /// <summary><c>AiUsageRecords."Model"</c>, part of the primary key.</summary>
    public const int Model = 128;

    /// <summary><c>AiUsageRecords."Kind"</c> — one of <c>AiUsageKinds</c>.</summary>
    public const int UsageKind = 16;

    /// <summary><c>OrganizationQuotas."Note"</c> — the operator's free text.</summary>
    public const int QuotaNote = 1000;
}
