using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Gamification.Common.Constants;

namespace Sellevate.Gamification.Infrastructure.Data;

/// <summary>
/// Builds a context for <c>dotnet ef</c>, which runs with no host and no configuration provider.
///
/// <para>
/// Design time has no request and therefore no organization. System mode keeps the tenant query
/// filters evaluating against a null organization instead of throwing, which is all
/// <c>dotnet ef migrations add</c> needs (mirrors <c>CompanyDbContextFactory</c> from 40.12).
/// </para>
///
/// <para>
/// The connection string is read from the environment, falling back to a localhost default. Nothing
/// here is reachable from the running service — it exists only so a developer can scaffold a
/// migration without exporting a variable first.
/// </para>
/// </summary>
internal sealed class GamificationDbContextFactory : IDesignTimeDbContextFactory<GamificationDbContext>
{
    public GamificationDbContext CreateDbContext(string[] arguments)
    {
        var optionsBuilder = new DbContextOptionsBuilder<GamificationDbContext>();
        var connectionString =
            Environment.GetEnvironmentVariable(ConfigurationKeys.PostgresConnectionEnvironmentVariable)
            ?? ConfigurationKeys.DesignTimePostgresConnectionString;
        optionsBuilder.UseNpgsql(connectionString);

        var designTimeTenantContext = new TenantContext();
        designTimeTenantContext.EnterSystemMode();

        return new GamificationDbContext(optionsBuilder.Options, designTimeTenantContext);
    }
}
