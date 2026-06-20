using Microsoft.EntityFrameworkCore;
using Sellevate.Gamification.Infrastructure.Data;

namespace Sellevate.Gamification.Tests.Unit;

internal static class GamificationDbContextFactory
{
    public static GamificationDbContext CreateInMemory()
    {
        var options = new DbContextOptionsBuilder<GamificationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new GamificationDbContext(options);
    }
}
