namespace Sellevate.Ai.Features.Quotas.Constants;

/// <summary>
/// The metered resource names carried by <c>AiQuotaExceededException.Resource</c>, by the
/// <c>resource</c> field of the 429 body, and by the <c>resource</c> Prometheus label. Wire contract:
/// see <c>docs/API_CONTRACTS.md</c> and <c>docs/MONITORING.md</c>.
/// </summary>
public static class AiQuotaResources
{
    public const string LlmTokens = "llm_tokens";
    public const string VoiceMinutes = "voice_minutes";
}
