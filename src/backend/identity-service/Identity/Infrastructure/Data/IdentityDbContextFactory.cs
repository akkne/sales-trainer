using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Identity.Infrastructure.Data;

/// <summary>
/// Builds an <see cref="IdentityDbContext"/> for <c>dotnet ef</c> only. Design time never runs a
/// tenant query — the tooling builds the model to diff it — so the context is handed a system-mode
/// <see cref="TenantContext"/> with no organization. Nothing at runtime resolves this factory.
/// </summary>
internal sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] arguments)
    {
        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5432;Database=identity;Username=postgres;Password=postgres";
        optionsBuilder.UseNpgsql(connectionString);

        var designTimeTenantContext = new TenantContext();
        designTimeTenantContext.EnterSystemMode();

        return new IdentityDbContext(optionsBuilder.Options, designTimeTenantContext);
    }
}
