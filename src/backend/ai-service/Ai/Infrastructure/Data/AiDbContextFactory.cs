using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sellevate.Ai.Infrastructure.Data;

internal sealed class AiDbContextFactory : IDesignTimeDbContextFactory<AiDbContext>
{
    public AiDbContext CreateDbContext(string[] arguments)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AiDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5432;Database=ai;Username=postgres;Password=postgres";
        optionsBuilder.UseNpgsql(connectionString);
        return new AiDbContext(optionsBuilder.Options);
    }
}
