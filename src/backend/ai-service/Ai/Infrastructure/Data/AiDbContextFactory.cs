using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Ai.Infrastructure.Data;

internal sealed class AiDbContextFactory : IDesignTimeDbContextFactory<AiDbContext>
{
    public AiDbContext CreateDbContext(string[] arguments)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AiDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5432;Database=ai;Username=postgres;Password=postgres";
        optionsBuilder.UseNpgsql(connectionString);

        // Design time has no request and therefore no organization. System mode keeps the tenant
        // query filters evaluating against a null organization instead of throwing, which is all
        // "dotnet ef migrations add" needs (mirrors LearningDbContextFactory from 40.10).
        var designTimeTenantContext = new TenantContext();
        designTimeTenantContext.EnterSystemMode();

        return new AiDbContext(optionsBuilder.Options, designTimeTenantContext);
    }
}
