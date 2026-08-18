using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Social.Infrastructure.Data;

/// <summary>
/// Builds a context for <c>dotnet ef</c> only — never for the running application, which gets its
/// context from dependency injection.
///
/// <para>
/// Design time has no request and therefore no organization, so the tenant context is put into system
/// mode: the query filters then evaluate against a null organization instead of throwing, which is all
/// a migration needs. It mirrors <c>CompanyDbContextFactory</c> from 40.12. The fallback connection
/// string points at a local developer database and is never a production target.
/// </para>
/// </summary>
internal sealed class SocialDbContextFactory : IDesignTimeDbContextFactory<SocialDbContext>
{
    public SocialDbContext CreateDbContext(string[] arguments)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SocialDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5432;Database=social;Username=postgres;Password=postgres";
        optionsBuilder.UseNpgsql(connectionString);

        var designTimeTenantContext = new TenantContext();
        designTimeTenantContext.EnterSystemMode();

        return new SocialDbContext(optionsBuilder.Options, designTimeTenantContext);
    }
}
