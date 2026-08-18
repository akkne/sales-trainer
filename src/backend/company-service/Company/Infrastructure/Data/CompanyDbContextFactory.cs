using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Company.Common.Constants;

namespace Sellevate.Company.Infrastructure.Data;

/// <summary>
/// Builds a <see cref="CompanyDbContext"/> for <c>dotnet ef</c>, which runs outside the application
/// and therefore without its configuration pipeline or its request scope.
///
/// <para>
/// Design time has no request and so no organization. The context enters system mode, which keeps
/// the tenant query filters evaluating against a null organization instead of throwing — all that
/// <c>migrations add</c> needs, since it only reads the model. Mirrors
/// <c>LearningDbContextFactory</c> from Phase 40.10.
/// </para>
///
/// <para>
/// The localhost fallback exists so a developer can scaffold a migration without exporting anything;
/// it points at the local dev Postgres and is never reachable from a deployed environment.
/// </para>
/// </summary>
public sealed class CompanyDbContextFactory : IDesignTimeDbContextFactory<CompanyDbContext>
{
    private const string LocalDevelopmentConnectionString =
        "Host=localhost;Port=5432;Database=company;Username=postgres;Password=postgres";

    public CompanyDbContext CreateDbContext(string[] arguments)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CompanyDbContext>();
        var connectionString =
            Environment.GetEnvironmentVariable(CompanyConfigurationKeys.PostgresConnectionEnvironmentVariable)
            ?? LocalDevelopmentConnectionString;
        optionsBuilder.UseNpgsql(connectionString);

        var designTimeTenantContext = new TenantContext();
        designTimeTenantContext.EnterSystemMode();

        return new CompanyDbContext(optionsBuilder.Options, designTimeTenantContext);
    }
}
