using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Social.Identity;
using Sellevate.Social.Infrastructure.Data;

namespace Sellevate.Social.Tests.Helpers;

internal static class TestSocialDatabaseFactory
{
    /// <summary>
    /// Phase 40.13. Unit tests run inside one organization by default. The in-memory provider has no
    /// row-level security, so the isolation guarantee is not what they check — they need a tenant
    /// context the query filters can resolve to something, plus the write guard so rows are stamped
    /// the way the real service stamps them. Mirrors <c>Ai.Tests/Unit/AiDbContextFactory</c> (40.11)
    /// and <c>Company.Tests</c>' factory (40.12).
    /// </summary>
    public static readonly Guid DefaultOrganizationId = new("50c1a100-0000-4000-8000-00000000a111");

    public static SocialDbContext CreateInMemory(Guid? organizationId = null)
        => CreateInMemory($"social-tests-{Guid.NewGuid()}", organizationId);

    public static SocialDbContext CreateInMemory(string databaseName, Guid? organizationId = null)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetOrganization(organizationId ?? DefaultOrganizationId);

        var options = new DbContextOptionsBuilder<SocialDbContext>()
            .UseInMemoryDatabase(databaseName)
            .AddInterceptors(new TenantSaveChangesInterceptor(tenantContext))
            .EnableSensitiveDataLogging()
            .Options;

        return new SocialDbContext(options, tenantContext);
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
