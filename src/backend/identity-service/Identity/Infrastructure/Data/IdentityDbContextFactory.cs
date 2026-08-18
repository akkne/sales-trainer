using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Identity.Infrastructure.Data;

internal sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] arguments)
    {
        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5432;Database=identity;Username=postgres;Password=postgres";
        optionsBuilder.UseNpgsql(connectionString);

        // Design time only: `dotnet ef` builds the model to diff it, never runs a tenant query, so
        // the tenant context is a system-mode one with no organization.
        var designTimeTenantContext = new TenantContext();
        designTimeTenantContext.EnterSystemMode();

        return new IdentityDbContext(optionsBuilder.Options, designTimeTenantContext);
    }
}
