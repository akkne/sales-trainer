using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.DependencyInjection;
using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Learning.Infrastructure.Data;

/// <summary>
/// Registers <see cref="LearningDbContext"/> together with the tenancy primitives it closes over.
///
/// <para>
/// Phase 40.10: learning-db is the first database where tenant data (progress) and the global content
/// library live side by side, so the context carries both tenancy interceptors — the write guard and
/// the one that issues <c>SET LOCAL app.organization_id</c> for the row-level-security policies. That
/// is why <c>AddSellevateTenancy</c> must run before the context is registered: the interceptors are
/// resolved out of the same container.
/// </para>
///
/// <para>
/// Never switch this to EF Core's pooled-context helper (<c>AddDbContextPool</c>). A pooled instance
/// is reused across unrelated requests along with everything it closed over at construction time,
/// including the <c>ITenantContext</c>-backed global query filter, so the first organization to touch
/// a pooled context would leak its filter onto every later caller. See docs/CODESTYLE.md §6 and
/// <c>scripts/tenancy-pool-lint.py</c>.
/// </para>
/// </summary>
public static class LearningDataAccessServiceCollectionExtensions
{
    private const string PostgresConnectionStringName = "Postgres";

    public static IServiceCollection AddLearningDataAccess(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSellevateTenancy();

        services.AddDbContext<LearningDbContext>((serviceProvider, databaseOptions) =>
            databaseOptions
                .UseNpgsql(configuration.GetConnectionString(PostgresConnectionStringName))
                .AddInterceptors(
                    serviceProvider.GetRequiredService<TenantSaveChangesInterceptor>(),
                    serviceProvider.GetRequiredService<TenantConnectionInterceptor>()));

        return services;
    }
}
