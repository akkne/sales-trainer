using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sellevate.Company.Infrastructure.Data;

public sealed class CompanyDbContextFactory : IDesignTimeDbContextFactory<CompanyDbContext>
{
    public CompanyDbContext CreateDbContext(string[] arguments)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CompanyDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=company;Username=postgres;Password=postgres");
        return new CompanyDbContext(optionsBuilder.Options);
    }
}
