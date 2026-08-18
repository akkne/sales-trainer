using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Ai.Infrastructure.Data;

/// <summary>
/// Builds an <see cref="AiDbContext"/> for <c>dotnet ef</c>, which runs with no host and therefore no
/// request.
///
/// <para>
/// Design time has no organization, so the context is entered in system mode: the tenant query filters
/// then evaluate against a null organization instead of throwing, which is all
/// <c>dotnet ef migrations add</c> needs. Mirrors <c>LearningDbContextFactory</c> from 40.10. Never
/// reached at runtime, which is why the fallback connection string may be a local default.
/// </para>
/// </summary>
internal sealed class AiDbContextFactory : IDesignTimeDbContextFactory<AiDbContext>
{
    public AiDbContext CreateDbContext(string[] arguments)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AiDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5432;Database=ai;Username=postgres;Password=postgres";
        optionsBuilder.UseNpgsql(connectionString);

        var designTimeTenantContext = new TenantContext();
        designTimeTenantContext.EnterSystemMode();

        return new AiDbContext(optionsBuilder.Options, designTimeTenantContext);
    }
}
