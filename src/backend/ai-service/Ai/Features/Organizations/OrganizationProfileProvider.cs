using Microsoft.EntityFrameworkCore;
using Sellevate.Ai.Infrastructure.Data;
using Sellevate.BuildingBlocks.ContentTemplating;
using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Ai.Features.Organizations;

/// <summary>
/// Phase 40.19. Reads <c>OrganizationProfileReplicas</c> once per request and remembers the answer:
/// one dialog turn builds a chat prompt, and completing a session builds a feedback prompt, both
/// from a row that cannot change mid-request.
///
/// <para>
/// <b>Platform-wide callers get the empty profile.</b> In platform mode the query filter admits
/// every organization at once, so "the profile" is not well defined and picking a row would mean
/// running a Sellevate staff practice call under some customer's compliance list. Same rule
/// <c>DialogModeOverrideResolution</c> follows for overrides.
/// </para>
/// </summary>
internal sealed class OrganizationProfileProvider(
    AiDbContext databaseContext,
    ITenantContext tenantContext) : IOrganizationProfileProvider
{
    private OrganizationProfileSnapshot? _cached;

    public async Task<OrganizationProfileSnapshot> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        if (tenantContext.OrganizationId is not { } organizationId || tenantContext.IsPlatformWide)
        {
            _cached = OrganizationProfileSnapshot.Empty;
            return _cached;
        }

        await using var tenantScope = await AiTenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        var replica = await databaseContext.OrganizationProfileReplicas
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.OrganizationId == organizationId, cancellationToken);

        _cached = replica is null
            ? OrganizationProfileSnapshot.Empty
            : OrganizationProfileSnapshot.FromJson(
                replica.Product,
                replica.Icp,
                replica.Tone,
                replica.ObjectionsJson,
                replica.ScriptJson,
                replica.GlossaryJson,
                replica.BannedClaimsJson);

        return _cached;
    }
}
