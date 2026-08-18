using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.BuildingBlocks.Tests.Tenancy.Support;

internal static class TenantTestDatabaseFactory
{
    public static TenantTestDbContext CreateInMemory(
        string databaseName,
        ITenantContext tenantContext,
        TenantSaveChangesInterceptor? interceptor = null)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TenantTestDbContext>()
            .UseInMemoryDatabase(databaseName)
            .EnableSensitiveDataLogging();

        if (interceptor is not null)
        {
            optionsBuilder.AddInterceptors(interceptor);
        }

        return new TenantTestDbContext(optionsBuilder.Options, tenantContext);
    }
}
