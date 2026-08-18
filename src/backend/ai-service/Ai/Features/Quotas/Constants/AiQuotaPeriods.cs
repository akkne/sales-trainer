namespace Sellevate.Ai.Features.Quotas.Constants;

/// <summary>
/// The window names carried by <c>AiQuotaExceededException.Period</c>, by the <c>period</c> field of
/// the 429 body, and by the <c>period</c> Prometheus label. <see cref="MonthBatchReserve"/> is the
/// one that means "a background pipeline stopped while conversations kept running" — a deliberate
/// degradation order, not a fault.
/// </summary>
public static class AiQuotaPeriods
{
    public const string Day = "day";
    public const string Month = "month";
    public const string MonthBatchReserve = "month_batch_reserve";
}
