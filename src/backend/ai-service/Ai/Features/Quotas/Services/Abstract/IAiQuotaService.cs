using Sellevate.Ai.Features.Quotas.Models;

namespace Sellevate.Ai.Features.Quotas.Services.Abstract;

/// <summary>
/// Phase 40.33. Resolves and edits one organization's allowance. Separate from
/// <see cref="IAiSpendMeter"/> because the meter is on the hot path of every call and this is not.
/// </summary>
public interface IAiQuotaService
{
    /// <summary>The row's values where it has them, platform defaults everywhere else. Never null.</summary>
    Task<ResolvedAiQuota> ResolveAsync(CancellationToken cancellationToken = default);

    Task<AiQuotaSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<AiQuotaSettingsDto> SaveSettingsAsync(AiQuotaWriteModel model, CancellationToken cancellationToken = default);

    Task<AiSpendReportDto> GetSpendReportAsync(CancellationToken cancellationToken = default);
}
