using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Organization.Infrastructure.Data;

/// <summary>
/// Exists only so <c>dotnet ef migrations</c> can build a model without booting the application. The
/// connection string below is never opened by this type — EF needs a provider to pick a migrations
/// SQL dialect, not a reachable server — and the runtime connection comes from
/// <c>ConnectionStrings:Postgres</c> instead. The <c>TenantContext</c> handed over is likewise empty on
/// purpose: a design-time model must not be shaped by whose request built it.
/// </summary>
public sealed class OrganizationDbContextFactory : IDesignTimeDbContextFactory<OrganizationDbContext>
{
    public OrganizationDbContext CreateDbContext(string[] arguments)
    {
        var optionsBuilder = new DbContextOptionsBuilder<OrganizationDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=organization;Username=postgres;Password=postgres");
        return new OrganizationDbContext(optionsBuilder.Options, new TenantContext());
    }
}
