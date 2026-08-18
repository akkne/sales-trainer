using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.ContentTemplating;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Features.Content.Services.Abstract;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.Content.Services.Implementation;

/// <summary>
/// Phase 40.19. Reads <c>OrganizationProfileReplicas</c> once per request and remembers the answer.
///
/// <para>
/// Scoped, and memoized inside the scope, because a single lesson open resolves placeholders in a
/// title, in every exercise of the lesson and possibly in a grading prompt. Without the memo that
/// is one query per rendered string for a row that cannot change mid-request.
/// </para>
///
/// <para>
/// <b>Platform-wide callers get the empty profile, deliberately.</b> In platform mode the query
/// filter admits every organization's rows at once, so "the profile" is not a well-defined thing:
/// picking any row would render Sellevate staff a lesson with some customer's product name in it.
/// Staff read the library as it is written — the same rule <c>ContentOverrideResolution</c> follows
/// for overrides.
/// </para>
/// </summary>
internal sealed class OrganizationProfileProvider(
    LearningDbContext databaseContext,
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

        await using var tenantScope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

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
