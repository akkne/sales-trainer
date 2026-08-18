namespace Sellevate.Ai.Features.Quotas.Constants;

/// <summary>
/// The fixed arithmetic the meter and the spend report share. These are invariants of the stored
/// shape rather than tuning: changing <see cref="MaximumBatchReservePercent"/> would reinterpret
/// every <c>OrganizationQuotas.BatchReservePercent</c> already written, and the other three are
/// definitions of their units.
/// </summary>
public static class AiQuotaScales
{
    /// <summary>Denominator every percent in this feature is expressed against.</summary>
    public const int PercentScale = 100;

    /// <summary>
    /// Ceiling on the batch reserve, applied both when an operator writes it and when the meter reads
    /// it. A reserve above this would leave batch work almost no allowance at all, which is a way of
    /// disabling the background pipelines by accident rather than a limit anybody wants.
    /// </summary>
    public const int MaximumBatchReservePercent = 90;

    /// <summary>Divisor of the per-million price table entries.</summary>
    public const decimal PriceUnitTokens = 1_000_000m;

    /// <summary>Seconds in one minute — the voice counters store seconds and report minutes.</summary>
    public const int SecondsPerMinute = 60;
}
