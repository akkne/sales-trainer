using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Gamification.Infrastructure.Data;

namespace Sellevate.Gamification.Tests.Unit;

/// <summary>
/// Builds in-memory contexts for the unit tests.
///
/// <para>
/// Two provider details are configured deliberately. The in-memory provider does not support
/// transactions, so the transaction-ignored warning is suppressed — otherwise
/// <c>LeagueService.CloseCurrentLeagueAndCreateNextAsync</c> would throw the moment it opened its
/// concurrency guard. And the real <c>TenantSaveChangesInterceptor</c> is installed rather than a
/// stand-in: it is what stamps <c>OrganizationId</c> onto rows the services create without naming one,
/// and what raises <c>CrossTenantWriteException</c> on a foreign one. Leaving it out would make every
/// unit test pass with <c>Guid.Empty</c>.
/// </para>
/// </summary>
internal static class GamificationDbContextFactory
{
    /// <summary>
    /// Phase 40.13. The organization every unit-test row belongs to unless a test names another
    /// one. Existing tests keep passing unchanged because the interceptor stamps this id on
    /// everything they insert and the query filter matches it back.
    /// </summary>
    public static readonly Guid DefaultOrganizationId = Guid.Parse("0d9b8f8e-0000-4000-8000-000000000013");

    public static GamificationDbContext CreateInMemory(string? databaseName = null, Guid? organizationId = null)
        => CreateInMemory(BuildTenantContext(organizationId), databaseName);

    /// <summary>
    /// InMemory context for a caller that wants to control the tenant context itself — a second
    /// organization sharing one database (the isolation tests), or no organization at all.
    /// </summary>
    public static GamificationDbContext CreateInMemory(ITenantContext tenantContext, string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<GamificationDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(new TenantSaveChangesInterceptor(tenantContext))
            .Options;

        return new GamificationDbContext(options, tenantContext);
    }

    public static TenantContext BuildTenantContext(Guid? organizationId = null)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetOrganization(organizationId ?? DefaultOrganizationId);
        return tenantContext;
    }
}
