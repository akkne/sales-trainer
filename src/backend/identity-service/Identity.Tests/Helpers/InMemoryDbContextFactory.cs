using Microsoft.EntityFrameworkCore;
using Sellevate.Identity.Infrastructure.Data;

namespace Sellevate.Identity.Tests.Helpers;

/// <summary>Builds an isolated in-memory <see cref="IdentityDbContext"/> for fast unit tests.</summary>
public static class InMemoryDbContextFactory
{
    public static IdentityDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;
        return new IdentityDbContext(options);
    }
}
