using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Company.Infrastructure.Data;

public sealed class CompanyDbContextFactory : IDesignTimeDbContextFactory<CompanyDbContext>
{
    public CompanyDbContext CreateDbContext(string[] arguments)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CompanyDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5432;Database=company;Username=postgres;Password=postgres";
        optionsBuilder.UseNpgsql(connectionString);

        // Design time has no request and therefore no organization. System mode keeps the tenant
        // query filters evaluating against a null organization instead of throwing, which is all
        // "dotnet ef migrations add" needs (mirrors LearningDbContextFactory from 40.10).
        var designTimeTenantContext = new TenantContext();
        designTimeTenantContext.EnterSystemMode();

        return new CompanyDbContext(optionsBuilder.Options, designTimeTenantContext);
    }
}
