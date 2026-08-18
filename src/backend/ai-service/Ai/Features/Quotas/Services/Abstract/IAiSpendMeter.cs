using Sellevate.Ai.Features.Quotas.Models;

namespace Sellevate.Ai.Features.Quotas.Services.Abstract;

/// <summary>
/// Phase 40.33. The one thing every LLM and speech call in the product passes through.
///
/// <para>
/// The gate is deliberately separate from the charge, because the two cannot be one operation: the
/// price of a completion is not known until it has been produced. So the meter reads the ledger
/// before the call and adds to it after, and the accepted consequence is stated rather than hidden —
/// concurrent calls that all pass the gate at 99% can overshoot by one call each. The overshoot is
/// bounded by concurrency and self-corrects on the next gate; the alternative (reserving a
/// worst-case token budget per call and refunding it) would refuse work an organization could
/// afford, every time, in exchange for a bound nobody needs.
/// </para>
/// </summary>
public interface IAiSpendMeter
{
    /// <summary>
    /// Refuses the call when the organization has spent its monthly LLM allowance — or, for
    /// <see cref="AiWorkloadClass.Batch"/>, when it has spent everything outside the reserve.
    /// Throws <see cref="AiQuotaExceededException"/>, or <see cref="AiUnattributedCallException"/>
    /// when the request carries no organization at all.
    /// </summary>
    Task EnsureLlmAllowanceAsync(string operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// The gate without the throw, for callers that need to decide rather than fail — the background
    /// sweeps ask this before they claim a lease, so a refused organization spends no attempt and
    /// stamps no lease. Returns false when the workload class is out of allowance.
    /// </summary>
    Task<bool> HasLlmAllowanceAsync(AiWorkloadClass workloadClass, CancellationToken cancellationToken = default);

    /// <summary>Adds one completion to the ledger. Never throws: a lost meter write must not fail a call already paid for.</summary>
    Task RecordLlmUsageAsync(
        string model,
        int promptTokens,
        int completionTokens,
        bool wasEstimated,
        CancellationToken cancellationToken = default);

    /// <summary>Adds synthesized or transcribed characters to the ledger. Never throws.</summary>
    Task RecordSpeechUsageAsync(
        string kind,
        string provider,
        int characterCount,
        CancellationToken cancellationToken = default);

    /// <summary>Counts a completion whose tokens the provider did not report, from the text on both ends.</summary>
    int EstimateTokens(string text);

    /// <summary>The same estimate from a character count, for prompts assembled out of many parts.</summary>
    int EstimateTokensFromLength(int characterCount);

    /// <summary>
    /// The organization-wide half of the voice gate, layered under the per-user one that has existed
    /// since the feature shipped. Reserving here is what makes the roadmap's «один клиент, гоняющий
    /// голос сутками, деградирует только свою организацию» true: before this block a customer could
    /// spend an unbounded number of user-sized allowances by adding users.
    /// </summary>
    Task ReserveVoiceSecondsAsync(int seconds, CancellationToken cancellationToken = default);

    /// <summary>Returns the unused tail of a reservation. Never throws.</summary>
    Task RefundVoiceSecondsAsync(int seconds, CancellationToken cancellationToken = default);

    /// <summary>The organization's reserved-or-spent voice seconds in the current day and month windows.</summary>
    Task<(int DaySeconds, int MonthSeconds)> GetVoiceSecondsAsync(CancellationToken cancellationToken = default);

    /// <summary>The caller's declared workload class for this request, resolved once from the header.</summary>
    AiWorkloadClass WorkloadClass { get; }
}
