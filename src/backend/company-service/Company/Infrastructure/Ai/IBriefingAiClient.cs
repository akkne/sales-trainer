namespace Sellevate.Company.Infrastructure.Ai;

/// <summary>
/// Composes a pre-call briefing for a company via ai-service. Implementations persist nothing and
/// throw rather than return a partial briefing, so a caller may treat any returned result as
/// complete.
/// </summary>
public interface IBriefingAiClient
{
    Task<BriefingAiResult> GenerateBriefingAsync(
        BriefingAiRequest request,
        CancellationToken cancellationToken = default);
}
