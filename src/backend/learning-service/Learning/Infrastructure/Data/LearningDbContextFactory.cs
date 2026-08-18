using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Learning.Infrastructure.Data;

/// <summary>
/// Builds a context for <c>dotnet ef</c> only. Nothing in the running service resolves this.
///
/// <para>
/// Design time has no request and therefore no organization, so the context is put in <b>system
/// mode</b>: the tenant query filters then evaluate against a null organization instead of throwing,
/// which is all <c>dotnet ef migrations add</c> needs (mirrors <c>IdentityDbContextFactory</c> from
/// 40.7). The fallback connection string is a local developer default and is never used where
/// <c>ConnectionStrings__Postgres</c> is set.
/// </para>
/// </summary>
internal sealed class LearningDbContextFactory : IDesignTimeDbContextFactory<LearningDbContext>
{
    public LearningDbContext CreateDbContext(string[] arguments)
    {
        var optionsBuilder = new DbContextOptionsBuilder<LearningDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5432;Database=learning;Username=postgres;Password=postgres";
        optionsBuilder.UseNpgsql(connectionString);

        var designTimeTenantContext = new TenantContext();
        designTimeTenantContext.EnterSystemMode();

        return new LearningDbContext(optionsBuilder.Options, designTimeTenantContext);
    }
}
