using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Sellevate.Gamification.Infrastructure.Data;

namespace Sellevate.Gamification.Tests.Unit;

internal static class GamificationDbContextFactory
{
    public static GamificationDbContext CreateInMemory()
    {
        var options = new DbContextOptionsBuilder<GamificationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            // The in-memory provider does not support transactions; suppress the warning
            // so that LeagueService.CloseCurrentLeagueAndCreateNextAsync can call
            // BeginTransactionAsync without throwing in unit tests.
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new GamificationDbContext(options);
    }
}
