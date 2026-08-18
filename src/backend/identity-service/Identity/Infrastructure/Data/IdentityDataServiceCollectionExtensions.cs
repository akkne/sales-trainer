using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.DependencyInjection;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Identity.Common.Constants;

namespace Sellevate.Identity.Infrastructure.Data;

/// <summary>
/// Registers the tenancy primitives and <see cref="IdentityDbContext"/> together, because the context
/// is only correct with both interceptors attached.
///
/// <para>
/// Phase 40.7: identity-db gained its first tenant-scoped table (Invites), so the context carries the
/// write guard as well as the interceptor that issues <c>SET LOCAL app.organization_id</c> for the
/// row-level-security policy. <b>Never switch this registration to EF Core's pooled-context helper</b>
/// — a pooled instance would reuse the query filter it closed over at construction time across
/// requests for unrelated organizations (docs/CODESTYLE.md §6, <c>scripts/tenancy-pool-lint.py</c>).
/// </para>
/// </summary>
public static class IdentityDataServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityDataAccess(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSellevateTenancy();

        services.AddDbContext<IdentityDbContext>((serviceProvider, databaseOptions) =>
            databaseOptions
                .UseNpgsql(configuration.GetConnectionString(ConfigurationKeys.PostgresConnectionName))
                .AddInterceptors(
                    serviceProvider.GetRequiredService<TenantSaveChangesInterceptor>(),
                    serviceProvider.GetRequiredService<TenantConnectionInterceptor>()));

        return services;
    }
}
