using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Gamification.Infrastructure.Data;

internal sealed class GamificationDbContextFactory : IDesignTimeDbContextFactory<GamificationDbContext>
{
    public GamificationDbContext CreateDbContext(string[] arguments)
    {
        var optionsBuilder = new DbContextOptionsBuilder<GamificationDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5432;Database=gamification;Username=postgres;Password=postgres";
        optionsBuilder.UseNpgsql(connectionString);

        // Design time has no request and therefore no organization. System mode keeps the tenant
        // query filters evaluating against a null organization instead of throwing, which is all
        // "dotnet ef migrations add" needs (mirrors CompanyDbContextFactory from 40.12).
        var designTimeTenantContext = new TenantContext();
        designTimeTenantContext.EnterSystemMode();

        return new GamificationDbContext(optionsBuilder.Options, designTimeTenantContext);
    }
}
