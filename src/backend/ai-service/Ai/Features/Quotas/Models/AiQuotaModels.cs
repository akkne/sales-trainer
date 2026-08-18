using Sellevate.Ai.Features.Quotas.Constants;

namespace Sellevate.Ai.Features.Quotas.Models;

/// <summary>
/// Which kind of work is asking for permission. ai-service cannot infer this — an internal call
/// looks the same whether a learner is waiting on it or a nightly sweep made it — so the caller
/// declares it in the <c>X-Ai-Workload</c> header and the default is the safe one.
/// </summary>
public enum AiWorkloadClass
{
    /// <summary>Somebody is waiting: a dialog turn, a graded exercise, an admin pressing a button.</summary>
    Interactive,

    /// <summary>A background pipeline: content generation, batch tone adaptation, AI content review.</summary>
    Batch,
}

/// <summary>
/// The resolved allowance for one organization: the row's values where it has them, the platform
/// defaults everywhere else.
/// </summary>
public sealed record ResolvedAiQuota(
    int VoiceDailyLimitMinutes,
    int VoiceMonthlyLimitMinutes,
    long LlmMonthlyTokenLimit,
    int BatchReservePercent,
    bool IsOrganizationSpecific)
{
    /// <summary>
    /// The monthly token count past which <see cref="AiWorkloadClass.Batch"/> work is refused while
    /// interactive work runs on to <see cref="LlmMonthlyTokenLimit"/>. The clamp is applied on read as
    /// well as on write, because a row persisted before the ceiling existed may hold anything.
    /// </summary>
    public long BatchTokenCeiling
    {
        get
        {
            var reservePercent = Math.Clamp(BatchReservePercent, 0, AiQuotaScales.MaximumBatchReservePercent);
            return LlmMonthlyTokenLimit - (LlmMonthlyTokenLimit * reservePercent / AiQuotaScales.PercentScale);
        }
    }
}

/// <summary>
/// Phase 40.33. Raised when an organization has spent its allowance. Carries the numbers rather than
/// a sentence so the caller can render one and the log can state a fact.
///
/// <para>
/// <b>The refusal is hard, and the softness lives one level up.</b> The roadmap asks that one
/// customer running voice for a day degrade only their own organization; it does not ask that they
/// be allowed to run forever. What degrades gracefully is *which* work stops first: batch work is
/// refused at the reserve threshold while interactive work runs to the full limit, so an
/// organization that has burned its month loses its overnight content pipeline before it loses the
/// conversation a rep is in the middle of.
/// </para>
/// </summary>
public sealed class AiQuotaExceededException(string resource, string period, long used, long limit)
    : Exception($"Organization quota exceeded for {resource} ({period}): {used} of {limit}.")
{
    /// <summary><c>llm_tokens</c> or <c>voice_minutes</c>.</summary>
    public string Resource { get; } = resource;

    /// <summary><c>day</c>, <c>month</c>, or <c>month_batch_reserve</c>.</summary>
    public string Period { get; } = period;

    public long Used { get; } = used;

    public long Limit { get; } = limit;
}

/// <summary>
/// Raised when a metered call arrives with no organization on it. Phase 40.33 makes this a refusal
/// rather than a shrug: every caller of an internal ai-service route forwards
/// <c>X-Organization-Id</c> as of this block, so an unattributed call is a caller that was added
/// without reading how the meter works — and the honest answer to "whose budget does this come out
/// of?" is not "nobody's".
/// </summary>
public sealed class AiUnattributedCallException(string operation)
    : Exception($"An LLM or speech call for '{operation}' arrived with no organization. " +
                "Internal callers must forward the X-Organization-Id header.");
