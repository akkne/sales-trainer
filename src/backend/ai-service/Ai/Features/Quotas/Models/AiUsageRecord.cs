using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Ai.Features.Quotas.Models;

/// <summary>
/// Phase 40.33. What one organization spent on one model in one UTC month — the durable half of the
/// meter, and the row the spend report is built from.
///
/// <para>
/// <b>Postgres rather than Redis, unlike the voice gate right next to it.</b> The two have different
/// jobs. Voice reserves seconds before a stream and refunds the unused tail milliseconds later, many
/// times a minute, so it needs an in-memory counter and it already has a durable record of its own
/// in Mongo's <c>dialog_sessions</c>. LLM spend is written once per completion — next to a call that
/// takes seconds — and its whole point is to be readable next month, before the provider's invoice
/// arrives. A counter that a Redis eviction can silently zero is not that. The write is a single
/// <c>INSERT … ON CONFLICT DO UPDATE SET x = x + excluded.x</c>, so it is atomic under concurrency
/// without a read-modify-write anywhere, which is the same property 40.27 required of its claim.
/// </para>
///
/// <para>
/// <b>One row per model, not one row per organization.</b> Models differ in price by more than an
/// order of magnitude, so a single blended token total cannot be turned into money without lying.
/// Keeping the breakdown means the cost estimate is a sum of per-model products and the price table
/// can be corrected afterwards without rewriting history.
/// </para>
///
/// <para>
/// Money is deliberately **not** a column. Tokens and characters are what the provider bills and
/// what we can count exactly; a stored price is a guess frozen at write time. Cost is derived on
/// read from <c>AiQuotas:PricePerMillionTokens</c>, which is also why editing that table moves no
/// limit — the limit is counted in tokens.
/// </para>
/// </summary>
public sealed class AiUsageRecord : ITenantScoped
{
    public Guid OrganizationId { get; set; }

    /// <summary>The UTC month, <c>yyyy-MM</c>. A string because it is a bucket label, not a date.</summary>
    public string PeriodKey { get; set; } = string.Empty;

    /// <summary>
    /// The provider model for <see cref="AiUsageKinds.Llm"/> rows; the synthesis or transcription
    /// provider name otherwise. Part of the key.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>One of <see cref="AiUsageKinds"/>. Derived from <see cref="Model"/>, stored so the report need not guess.</summary>
    public string Kind { get; set; } = AiUsageKinds.Llm;

    public long PromptTokens { get; set; }

    public long CompletionTokens { get; set; }

    public long CallCount { get; set; }

    /// <summary>
    /// Calls whose token counts were estimated from characters because the provider reported no
    /// <c>usage</c> block — today, exactly the streamed dialog turns. Surfaced in the report so
    /// nobody reads an estimate as a measurement.
    /// </summary>
    public long EstimatedCallCount { get; set; }

    /// <summary>Characters sent to a speech provider (TTS) or transcribed from one (STT).</summary>
    public long SpeechCharacters { get; set; }

    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// The three kinds of spend the ledger distinguishes, and the value of <c>AiUsageRecord.Kind</c>. Persisted
/// and grouped on by the spend report, so these strings must not change.
/// </summary>
public static class AiUsageKinds
{
    public const string Llm = "llm";
    public const string Tts = "tts";
    public const string Stt = "stt";

    public static readonly string[] All = [Llm, Tts, Stt];
}
