using Prometheus;

namespace Sellevate.Ai.Infrastructure.Metrics;

/// <summary>
/// Phase 40.33. Platform-wide AI spend, exported for Prometheus so an operator sees the curve
/// bending before the provider's invoice arrives.
///
/// <para>
/// <b>No organization label, and that is the fifth time this codebase has made that call</b> — see
/// <c>docs/MONITORING.md</c> and analytics-service's <c>AppMetrics</c>, which records the same
/// refusal three times over. A customer id in a label puts identities and unbounded cardinality into
/// the monitoring store. Every label here is closed: <c>kind</c> is the three values in
/// <c>AiUsageKinds</c>, <c>resource</c> is the two things a quota can be about, and neither can grow
/// from data.
/// </para>
///
/// <para>
/// So these answer «сколько платформа сожгла и растёт ли это», and deliberately cannot answer «чей
/// это расход». That second question is answered by <c>GET /admin/ai-usage</c> from the
/// <c>AiUsageRecords</c> rows — the same split the assignment funnel uses (40.25): totals in
/// Prometheus, per-organization numbers from the owning service's tables, never the other way round.
/// </para>
///
/// <para>
/// This is also the first <c>/metrics</c> endpoint outside analytics-service and the gateway. It is
/// here rather than in analytics because analytics is <b>Redis-only and consumes Kafka</b> (40.16,
/// re-confirmed four times since): routing spend there would mean a new topic, a new consumer, and a
/// counter that lags the call it counts, in a service whose whole design point is that it owns no
/// relational state. ai-service already holds the numbers at the instant they happen.
/// </para>
/// </summary>
public static class AiSpendMetrics
{
    public static readonly Counter LlmTokens = Prometheus.Metrics.CreateCounter(
        "ai_llm_tokens_total",
        "LLM tokens spent across all organizations, split into prompt and completion.",
        new CounterConfiguration { LabelNames = ["direction"] });

    public static readonly Counter LlmCalls = Prometheus.Metrics.CreateCounter(
        "ai_llm_calls_total",
        "LLM completions across all organizations, split by whether the token count was reported or estimated.",
        new CounterConfiguration { LabelNames = ["accounting"] });

    public static readonly Counter SpeechCharacters = Prometheus.Metrics.CreateCounter(
        "ai_speech_characters_total",
        "Characters synthesized or transcribed across all organizations.",
        new CounterConfiguration { LabelNames = ["kind"] });

    /// <summary>
    /// Calls refused because an organization had spent its allowance. A rising line here is a
    /// commercial signal, not an incident — which is why the block that raises it logs at
    /// Information and this metric exists instead of an alert.
    /// </summary>
    public static readonly Counter QuotaRefusals = Prometheus.Metrics.CreateCounter(
        "ai_quota_refusals_total",
        "Calls refused because an organization reached its allowance.",
        new CounterConfiguration { LabelNames = ["resource", "period"] });
}
