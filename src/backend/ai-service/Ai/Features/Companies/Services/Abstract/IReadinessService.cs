using Sellevate.Ai.Features.Companies.Models;

namespace Sellevate.Ai.Features.Companies.Services.Abstract;

/// <summary>
/// Scores how ready a seller is for a real call, from the training sessions they have already held. The
/// only service in this feature that reads stored data, and it reads it scoped to the owning user.
/// </summary>
public interface IReadinessService
{
    /// <summary>
    /// Reads ai-service's own Mongo <c>DialogSession</c> documents for the supplied session ids,
    /// pulls each session's <c>Feedback.Summary</c> (skipping sessions with no feedback yet, e.g.
    /// abandoned calls), and asks the configured LLM to score sales-call readiness from those
    /// summaries. Returns null when no session had usable feedback — the "no data yet" signal the
    /// caller (company-service) turns into a 204.
    /// </summary>
    Task<ReadinessResultDto?> GenerateReadinessAsync(
        GenerateReadinessRequestDto request,
        CancellationToken cancellationToken = default);
}
