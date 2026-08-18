namespace Sellevate.Ai.Features.Quotas.Constants;

/// <summary>
/// The four values of <c>AiSpendReportDto.QuotaState</c>, in ascending severity. The third is the
/// informative one: <see cref="BatchPaused"/> means batch work has stopped while interactive work has
/// not, so an administrator whose content pipeline went quiet can tell that from an outage.
/// </summary>
public static class AiQuotaStates
{
    public const string Ok = "ok";
    public const string Warning = "warning";
    public const string BatchPaused = "batch_paused";
    public const string Exhausted = "exhausted";
}
