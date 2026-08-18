namespace Sellevate.Company.Infrastructure.Ai;

/// <summary>
/// Scores a salesperson's readiness for one company from the feedback on their practice sessions,
/// via ai-service. A null result is a first-class outcome and not an error — callers must keep it
/// distinct from a failure, because the two get opposite treatment (negative-cache versus 503).
/// </summary>
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
