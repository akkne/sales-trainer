namespace Sellevate.Company.Infrastructure.Ai;

public interface IBriefingAiClient
{
    Task<BriefingAiResult> GenerateBriefingAsync(
        BriefingAiRequest request,
        CancellationToken cancellationToken = default);
}
