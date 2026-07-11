namespace Sellevate.Company.Infrastructure.Ai;

public interface IReadinessAiClient
{
    /// <summary>
    /// Calls ai-service's internal readiness endpoint. Returns null when ai-service signals "no
    /// data yet" (204 — none of the supplied sessions had usable feedback).
    /// </summary>
    Task<ReadinessAiResult?> GenerateReadinessAsync(
        ReadinessAiRequest request,
        CancellationToken cancellationToken = default);
}
