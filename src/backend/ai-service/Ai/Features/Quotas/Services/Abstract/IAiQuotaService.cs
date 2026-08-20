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

    /// <summary>
    /// The named organization's own settings, read directly rather than through the caller's own
    /// <c>X-Organization-Id</c>. Platform staff already read across every organization elsewhere
    /// (<c>OrganizationQuota</c>'s query filter widens on <c>IsPlatformWide</c>); this exists so the
    /// platform panel's per-organization quota screen can show organization X's real numbers while
    /// looking at organization X, instead of silently substituting the caller's own organization
    /// (2026-08-21 admin audit, AD-5). Read-only: writing still requires
    /// <see cref="SaveSettingsAsync"/> and stays bound to the caller's own organization — see that
    /// method's remarks.
    /// </summary>
    Task<AiQuotaSettingsDto> GetSettingsForOrganizationAsync(
        Guid organizationId, CancellationToken cancellationToken = default);

    Task<AiQuotaSettingsDto> SaveSettingsAsync(AiQuotaWriteModel model, CancellationToken cancellationToken = default);

    Task<AiSpendReportDto> GetSpendReportAsync(CancellationToken cancellationToken = default);
}
