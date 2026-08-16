using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Social.Infrastructure.Data;

internal sealed class SocialDbContextFactory : IDesignTimeDbContextFactory<SocialDbContext>
{
    public SocialDbContext CreateDbContext(string[] arguments)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SocialDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5432;Database=social;Username=postgres;Password=postgres";
        optionsBuilder.UseNpgsql(connectionString);

        // Design time has no request and therefore no organization. System mode keeps the tenant
        // query filters evaluating against a null organization instead of throwing, which is all
        // "dotnet ef migrations add" needs (mirrors CompanyDbContextFactory from 40.12).
        var designTimeTenantContext = new TenantContext();
        designTimeTenantContext.EnterSystemMode();

        return new SocialDbContext(optionsBuilder.Options, designTimeTenantContext);
    }
}
