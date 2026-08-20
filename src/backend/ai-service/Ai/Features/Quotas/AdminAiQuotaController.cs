using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.Ai.Common.Constants;
using Sellevate.Ai.Features.Quotas.Models;
using Sellevate.Ai.Features.Quotas.Services.Abstract;
using Sellevate.Ai.Infrastructure.Data;
using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Ai.Features.Quotas;

/// <summary>
/// Phase 40.33. The spend report — «расход виден в дашборде раньше, чем в счёте от провайдера».
///
/// <para>
/// <b>Organization administrators, not platform staff only.</b> The person who has to know that
/// their content pipeline is about to stop is the РОП whose pipeline it is, and telling them a month
/// later through a support ticket is the situation the roadmap bullet exists to prevent. Rows are
/// their organization's, by the ordinary query filter — the same shape 40.25's transcript screen
/// uses, and the reason this is a separate controller from the platform-only one below.
/// </para>
///
/// <para>
/// A platform administrator with no organization header reads the installation-wide total, because
/// <c>AiUsageRecords</c> follows the codebase's <c>IsPlatformWide</c> widening (40.16: reads widen
/// for platform staff, writes widen nowhere). That is deliberate and it is the one cross-organization
/// total in this service — 40.11 removed the other one from <c>/admin/voice/usage</c> because it
/// leaked *user rows*; this returns per-model token counts and no identities at all.
/// </para>
/// </summary>
[ApiController]
[Route("admin/ai-usage")]
[Authorize(Policy = AuthorizationPolicies.RequireOrganizationAdministrator)]
[TenantScoped]
[TenantTransaction]
public sealed class AdminAiUsageController(IAiQuotaService quotaService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AiSpendReportDto>> GetSpend(CancellationToken cancellationToken)
        => Ok(await quotaService.GetSpendReportAsync(cancellationToken));
}

/// <summary>
/// Phase 40.33. Reading and writing an organization's allowance.
///
/// <para>
/// <b>Platform staff only, and that is a commercial boundary rather than a technical one.</b> A quota
/// is what the customer bought; an organization administrator raising their own is not an
/// administrative action, it is a purchase. <c>PUT</c> always writes the organization in the caller's
/// own <c>X-Organization-Id</c> header — for platform staff, the one they impersonated into (40.9) —
/// which is why the write has no organization id in the route and no body field carrying one
/// (<c>scripts/tenancy-boundary-lint.py</c>): a platform administrator's own session is never
/// scoped to a customer's organization, so today there is no token that both passes
/// <see cref="AuthorizationPolicies.RequirePlatformAdministrator"/> and writes anywhere but the
/// platform's own default organization. That gap is tracked as Q-10 in
/// docs/NIGHT_AUDIT_QUESTIONS.md; this controller does not attempt to close it.
/// </para>
///
/// <para>
/// <c>GET {id}</c> is the narrow exception: it names the organization explicitly, in the route, and
/// the tenancy-boundary lint allow-lists this file for exactly that reason (2026-08-21 admin audit,
/// AD-5). Doing so is safe for reads only — <c>OrganizationQuota</c>'s query filter already widens to
/// every organization for platform staff, so this simply narrows that already-wide read to one
/// organization instead of leaving it defaulted to the caller's own. It must never be mirrored onto
/// the <c>PUT</c>: the <c>TenantSaveChangesInterceptor</c> and the RLS <c>WITH CHECK</c> clause both
/// still require a write's <c>OrganizationId</c> to match the caller's own <see cref="ITenantContext"/>,
/// which no code in this controller may reassign mid-request.
/// </para>
/// </summary>
[ApiController]
[Route("admin/ai-quota")]
[Authorize(Policy = AuthorizationPolicies.RequirePlatformAdministrator)]
[TenantScoped]
[TenantTransaction]
public sealed class AdminAiQuotaController(IAiQuotaService quotaService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AiQuotaSettingsDto>> GetQuota(CancellationToken cancellationToken)
        => Ok(await quotaService.GetSettingsAsync(cancellationToken));

    /// <summary>
    /// The platform panel's per-organization quota screen
    /// (<c>/admin/organizations/{id}/quota</c>) calls this instead of the parameterless
    /// <see cref="GetQuota"/> so the numbers shown always belong to the organization the operator is
    /// actually looking at, not to whichever organization the caller's own session happens to carry
    /// (AD-5). There is deliberately no write counterpart — see the type-level remarks.
    /// </summary>
    [HttpGet("{organizationId:guid}")]
    public async Task<ActionResult<AiQuotaSettingsDto>> GetQuotaForOrganization(
        Guid organizationId, CancellationToken cancellationToken)
        => Ok(await quotaService.GetSettingsForOrganizationAsync(organizationId, cancellationToken));

    [HttpPut]
    public async Task<ActionResult<AiQuotaSettingsDto>> SaveQuota(
        [FromBody] AiQuotaWriteModel model,
        CancellationToken cancellationToken)
        => Ok(await quotaService.SaveSettingsAsync(model, cancellationToken));
}
