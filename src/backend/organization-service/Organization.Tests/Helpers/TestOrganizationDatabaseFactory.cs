using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory.Diagnostics.Internal;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Organization.Infrastructure.Data;

namespace Sellevate.Organization.Tests.Helpers;

internal static class TestOrganizationDatabaseFactory
{
    public static OrganizationDbContext CreateInMemory(ITenantContext? tenantContext = null, string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseInMemoryDatabase(databaseName ?? $"organization-tests-{Guid.NewGuid()}")
            .EnableSensitiveDataLogging()
            // OrganizationProfileService.GetProfileAsync wraps its read in an explicit transaction
            // so SET LOCAL app.organization_id has a transaction to scope to on real Postgres
            // (docs/TENANCY/TENANCY.md §1.5). The InMemory provider has no real transactions and
            // otherwise throws on BeginTransactionAsync purely as a test-double limitation — this
            // does not change how the real Npgsql provider behaves.
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new OrganizationDbContext(options, tenantContext ?? new TenantContext());
    }
}
