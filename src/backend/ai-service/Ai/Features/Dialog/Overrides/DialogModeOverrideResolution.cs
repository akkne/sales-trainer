using Microsoft.EntityFrameworkCore;
using Sellevate.Ai.Features.Dialog.Models;
using Sellevate.Ai.Infrastructure.Data;

namespace Sellevate.Ai.Features.Dialog.Overrides;

/// <summary>
/// Phase 40.18. Read resolution for dialog modes: an override exists → use it; otherwise → the
/// global prompt (docs/TENANCY/CONTENT_MODEL.md §1).
///
/// <para>
/// The 40.11 query filter admits "mine or global", so an organization that overrode one mode of a
/// bundle would see that mode listed twice — its own prompt and the base — inside the same bundle.
/// This is the missing half.
/// </para>
///
/// <para>
/// Applied to the learner-facing mode list only. The authoring endpoints keep seeing both sides,
/// because the review screen exists to show them side by side; and platform-wide callers do not
/// resolve, or one customer's override would hide a global mode from Sellevate staff.
/// </para>
/// </summary>
public static class DialogModeOverrideResolution
{
    public static IQueryable<DialogMode> ResolveOverrides(
        this IQueryable<DialogMode> modes,
        AiDbContext databaseContext)
    {
        ArgumentNullException.ThrowIfNull(modes);
        ArgumentNullException.ThrowIfNull(databaseContext);

        var tenantContext = databaseContext.TenantContext;

        if (tenantContext.OrganizationId is not { } organizationId || tenantContext.IsPlatformWide)
        {
            return modes;
        }

        return modes.Where(mode =>
            mode.OrganizationId != null
            || !databaseContext.DialogModes.Any(candidate =>
                candidate.ParentModeId == mode.Id && candidate.OrganizationId == organizationId));
    }
}
