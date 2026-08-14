using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Organization.Infrastructure.Data;

public sealed class OrganizationDbContextFactory : IDesignTimeDbContextFactory<OrganizationDbContext>
{
    public OrganizationDbContext CreateDbContext(string[] arguments)
    {
        var optionsBuilder = new DbContextOptionsBuilder<OrganizationDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=organization;Username=postgres;Password=postgres");
        return new OrganizationDbContext(optionsBuilder.Options, new TenantContext());
    }
}
