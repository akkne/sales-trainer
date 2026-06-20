using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sellevate.Social.Infrastructure.Data;

internal sealed class SocialDbContextFactory : IDesignTimeDbContextFactory<SocialDbContext>
{
    public SocialDbContext CreateDbContext(string[] arguments)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SocialDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5432;Database=social;Username=postgres;Password=postgres";
        optionsBuilder.UseNpgsql(connectionString);
        return new SocialDbContext(optionsBuilder.Options);
    }
}
