using Microsoft.EntityFrameworkCore;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Tests.Unit;

internal static class LearningDbContextFactory
{
    public static LearningDbContext CreateInMemory()
    {
        var options = new DbContextOptionsBuilder<LearningDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new LearningDbContext(options);
    }
}
