using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sellevate.Gamification.Infrastructure.Data;

internal sealed class GamificationDbContextFactory : IDesignTimeDbContextFactory<GamificationDbContext>
{
    public GamificationDbContext CreateDbContext(string[] arguments)
    {
        var optionsBuilder = new DbContextOptionsBuilder<GamificationDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5432;Database=gamification;Username=postgres;Password=postgres";
        optionsBuilder.UseNpgsql(connectionString);
        return new GamificationDbContext(optionsBuilder.Options);
    }
}
