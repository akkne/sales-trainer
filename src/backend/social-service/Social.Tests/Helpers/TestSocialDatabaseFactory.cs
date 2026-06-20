using Microsoft.EntityFrameworkCore;
using Sellevate.Social.Identity;
using Sellevate.Social.Infrastructure.Data;

namespace Sellevate.Social.Tests.Helpers;

internal static class TestSocialDatabaseFactory
{
    public static SocialDbContext CreateInMemory()
    {
        var options = new DbContextOptionsBuilder<SocialDbContext>()
            .UseInMemoryDatabase($"social-tests-{Guid.NewGuid()}")
            .EnableSensitiveDataLogging()
            .Options;

        return new SocialDbContext(options);
    }

    public static async Task<UserReplica> SeedUserAsync(
        SocialDbContext databaseContext,
        Guid userId,
        string displayName,
        string? email = null,
        CancellationToken cancellationToken = default)
    {
        var replica = new UserReplica
        {
            UserId = userId,
            DisplayName = displayName,
            Email = email ?? $"{userId:N}@example.com",
            UpdatedAt = DateTime.UtcNow
        };
        databaseContext.UserReplicas.Add(replica);
        await databaseContext.SaveChangesAsync(cancellationToken);
        return replica;
    }
}
