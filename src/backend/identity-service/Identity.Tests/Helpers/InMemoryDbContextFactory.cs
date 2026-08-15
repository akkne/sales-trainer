using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Identity.Infrastructure.Data;

namespace Sellevate.Identity.Tests.Helpers;

public static class InMemoryDbContextFactory
{
    public static IdentityDbContext Create(string? databaseName = null, ITenantContext? tenantContext = null)
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;
        return new IdentityDbContext(options, tenantContext ?? CreateSystemModeTenantContext());
    }

    private static TenantContext CreateSystemModeTenantContext()
    {
        var tenantContext = new TenantContext();
        tenantContext.EnterSystemMode();
        return tenantContext;
    }
}
