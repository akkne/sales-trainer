using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Learning.Infrastructure.Data;

internal sealed class LearningDbContextFactory : IDesignTimeDbContextFactory<LearningDbContext>
{
    public LearningDbContext CreateDbContext(string[] arguments)
    {
        var optionsBuilder = new DbContextOptionsBuilder<LearningDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5432;Database=learning;Username=postgres;Password=postgres";
        optionsBuilder.UseNpgsql(connectionString);

        // Design time has no request and therefore no organization. System mode keeps the tenant
        // query filters evaluating against a null organization instead of throwing, which is all
        // "dotnet ef migrations add" needs (mirrors IdentityDbContextFactory from 40.7).
        var designTimeTenantContext = new TenantContext();
        designTimeTenantContext.EnterSystemMode();

        return new LearningDbContext(optionsBuilder.Options, designTimeTenantContext);
    }
}
