using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory.Diagnostics.Internal;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Organization.Infrastructure.Data;

namespace Sellevate.Organization.Tests.Helpers;

/// <summary>
/// An <see cref="OrganizationDbContext"/> over the InMemory provider, wired the way the unit tests
/// need it.
///
/// <para>
/// <c>TransactionIgnoredWarning</c> is suppressed rather than worked around.
/// <c>OrganizationProfileService</c> wraps even its reads in an explicit transaction so
/// <c>SET LOCAL app.organization_id</c> has a transaction to scope to on real Postgres
/// (docs/TENANCY/TENANCY.md §1.5); the InMemory provider has no real transactions and otherwise
/// throws on <c>BeginTransactionAsync</c> purely as a test-double limitation. Suppressing it changes
/// nothing about how the real Npgsql provider behaves.
/// </para>
/// </summary>
internal static class TestOrganizationDatabaseFactory
{
    public static OrganizationDbContext CreateInMemory(ITenantContext? tenantContext = null, string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseInMemoryDatabase(databaseName ?? $"organization-tests-{Guid.NewGuid()}")
            .EnableSensitiveDataLogging()
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new OrganizationDbContext(options, tenantContext ?? new TenantContext());
    }
}
