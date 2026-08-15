using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Tests.Unit;

internal static class LearningDbContextFactory
{
    /// <summary>
    /// Phase 40.10. Unit tests run inside one organization by default. The in-memory provider has
    /// no row-level security, so the isolation guarantee is not what these tests check — they only
    /// need a tenant context that the query filters can resolve to something, and the write guard
    /// so tenant-scoped rows are stamped the way the real service stamps them.
    /// </summary>
    public static readonly Guid DefaultOrganizationId = new("aaaaaaaa-0000-4000-8000-000000000001");

    public static LearningDbContext CreateInMemory(Guid? organizationId = null)
        => CreateInMemory(Guid.NewGuid().ToString(), organizationId);

    public static LearningDbContext CreateInMemory(string databaseName, Guid? organizationId = null)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetOrganization(organizationId ?? DefaultOrganizationId);

        var options = new DbContextOptionsBuilder<LearningDbContext>()
            .UseInMemoryDatabase(databaseName)
            .AddInterceptors(new TenantSaveChangesInterceptor(tenantContext))
            .Options;

        return new LearningDbContext(options, tenantContext);
    }
}
