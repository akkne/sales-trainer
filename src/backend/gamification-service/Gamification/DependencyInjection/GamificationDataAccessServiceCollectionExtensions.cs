using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sellevate.BuildingBlocks.DependencyInjection;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Gamification.Common.Constants;
using Sellevate.Gamification.Infrastructure.Data;
using StackExchange.Redis;

namespace Sellevate.Gamification.DependencyInjection;

/// <summary>
/// Registers everything gamification-service talks to a datastore through: the tenancy primitives,
/// <see cref="GamificationDbContext"/>, and the Redis multiplexer.
/// </summary>
public static class GamificationDataAccessServiceCollectionExtensions
{
    /// <summary>
    /// Phase 40.13. Registers the request-scoped <c>ITenantContext</c> plus the two interceptors: the
    /// cross-tenant write guard and the one that issues <c>SET LOCAL app.organization_id</c> for the
    /// row-level-security policies. <c>AddSellevateEventing</c> also calls
    /// <c>AddSellevateTenancy</c>, but this method registers it explicitly so the <c>DbContext</c>
    /// registration cannot depend on the ordering of an unrelated call.
    ///
    /// <para>
    /// <b>Never switch to EF Core's pooled-context helper</b> (docs/CODESTYLE.md,
    /// <c>scripts/tenancy-pool-lint.py</c>) — a pooled context would cache the first tenant's query
    /// filter and hand it to every later caller.
    /// </para>
    ///
    /// <para>
    /// The Npgsql session timezone is pinned to UTC so that
    /// <c>DateOnly.FromDateTime(timestamptz)</c> comparisons in the experience-point sync queries
    /// always bucket dates consistently, regardless of the host OS timezone.
    /// </para>
    /// </summary>
    public static IServiceCollection AddGamificationDataAccess(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSellevateTenancy();

        services.AddDbContext<GamificationDbContext>((serviceProvider, databaseOptions) =>
        {
            var connectionString = configuration.GetConnectionString(ConfigurationKeys.PostgresConnectionName);
            var utcConnectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString)
            {
                Timezone = UtcTimezone,
            };
            databaseOptions
                .UseNpgsql(utcConnectionStringBuilder.ConnectionString)
                .AddInterceptors(
                    serviceProvider.GetRequiredService<TenantSaveChangesInterceptor>(),
                    serviceProvider.GetRequiredService<TenantConnectionInterceptor>());
        });

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(
                configuration.GetConnectionString(ConfigurationKeys.RedisConnectionName)!));

        return services;
    }

    private const string UtcTimezone = "UTC";
}
