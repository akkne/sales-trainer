using Microsoft.EntityFrameworkCore;
using Sellevate.Ai.Infrastructure.Data;
using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Ai.Tests.Unit;

internal static class AiDbContextFactory
{
    /// <summary>
    /// Phase 40.11. Unit tests run inside one organization by default. The in-memory provider has
    /// no row-level security, so the isolation guarantee is not what these tests check — they only
    /// need a tenant context the query filters can resolve to something, plus the write guard so
    /// rows are stamped the way the real service stamps them. Mirrors
    /// <c>Learning.Tests/Unit/LearningDbContextFactory</c> from 40.10.
    /// </summary>
    public static readonly Guid DefaultOrganizationId = new("aaaaaaaa-0000-4000-8000-00000000a111");

    public static AiDbContext CreateInMemory(Guid? organizationId = null)
        => CreateInMemory(Guid.NewGuid().ToString(), organizationId);

    public static AiDbContext CreateInMemory(string databaseName, Guid? organizationId = null)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetOrganization(organizationId ?? DefaultOrganizationId);

        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase(databaseName)
            .AddInterceptors(new TenantSaveChangesInterceptor(tenantContext))
            .Options;

        return new AiDbContext(options, tenantContext);
    }
}
