namespace Sellevate.Ai.Infrastructure.Configuration;

/// <summary>
/// Phase 40.33. The platform-wide defaults every organization is metered against until somebody
/// gives it numbers of its own, plus the price table the spend report is rendered with.
///
/// <para>
/// <b>An absent <c>OrganizationQuotas</c> row does not mean "no limit".</b> It means "the numbers in
/// this section", which is exactly the behaviour ai-service had before this block — voice limits
/// came from <c>Voice:DailyLimitMinutes</c> / <c>MonthlyLimitMinutes</c> and nothing else existed.
/// That is what dissolves the fail-open / fail-closed argument the roadmap poses: a customer whose
/// row has not been written is still metered, just against the default, so neither a missing row nor
/// a lagging replica can hand anybody an unmetered night of voice.
/// </para>
///
/// <para>
/// <b>The price table never enforces anything.</b> Limits are counted in tokens and minutes, which
/// are what the provider actually bills us in and what we can count exactly; money is derived for
/// display only. Editing a price here re-renders history and moves no limit, which is the property
/// that lets an operator correct a stale price without silently tightening or loosening every
/// customer's cap.
/// </para>
/// </summary>
public sealed class AiQuotaConfiguration
{
    public const string SectionName = "AiQuotas";

    /// <summary>Voice minutes an organization may spend in one UTC day. 0 disables the day window.</summary>
    public int DefaultVoiceDailyLimitMinutes { get; init; } = 600;

    /// <summary>Voice minutes an organization may spend in one UTC month. 0 disables the month window.</summary>
    public int DefaultVoiceMonthlyLimitMinutes { get; init; } = 6000;

    /// <summary>
    /// Prompt + completion tokens an organization may spend in one UTC month across every model.
    /// 0 disables the LLM limit entirely.
    /// </summary>
    public long DefaultLlmMonthlyTokenLimit { get; init; } = 20_000_000;

    /// <summary>
    /// The share of the monthly LLM allowance that batch work may not touch. At 10, a background
    /// pipeline stops at 90% while a learner mid-conversation keeps going to 100% — the roadmap's
    /// "деградирует только свою организацию" applied inside one organization too, so the thing that
    /// stops first is the РОП's overnight batch rather than twenty reps on a call.
    /// </summary>
    public int DefaultBatchReservePercent { get; init; } = 10;

    /// <summary>
    /// Warning threshold, in percent of the monthly LLM allowance. Crossing it refuses nothing; it
    /// raises the <c>quotaState</c> the spend report carries so somebody sees the wall coming.
    /// </summary>
    public int SoftWarningPercent { get; init; } = 80;

    /// <summary>Currency the derived cost estimate is rendered in. Display only.</summary>
    public string Currency { get; init; } = "RUB";

    /// <summary>
    /// Price per one million tokens, keyed by model name, plus the reserved keys <c>tts</c>
    /// (per million synthesized characters) and <c>stt</c> (per million transcribed characters).
    /// A model missing from the table is priced with <see cref="FallbackPricePerMillionTokens"/>
    /// and reported as such rather than silently as zero.
    /// </summary>
    public Dictionary<string, decimal> PricePerMillionTokens { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public decimal FallbackPricePerMillionTokens { get; init; } = 0m;

    /// <summary>
    /// Divisor turning characters into estimated tokens on the one path where the provider reports
    /// no usage: a streamed dialog turn. Four characters per token is the usual Latin ratio and is
    /// pessimistic for Russian, which is the direction an estimate on a metered path should err in.
    /// </summary>
    public int EstimatedCharactersPerToken { get; init; } = 4;
}
