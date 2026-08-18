using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using Sellevate.Ai.Infrastructure.Mongo;
using Sellevate.BuildingBlocks.DependencyInjection;
using Sellevate.BuildingBlocks.Tenancy;
using StackExchange.Redis;

namespace Sellevate.Ai.Infrastructure.Data;

/// <summary>
/// Registers ai-service's three stores: Postgres for the dialog library and the quota ledger, Mongo for
/// dialog sessions, Redis for the voice counters and the audio cache.
///
/// <para>
/// <b>The order inside this method is load-bearing and no test catches it.</b>
/// <c>AddSellevateTenancy</c> must run before <c>AddDbContext</c>, because the context's interceptors
/// are resolved out of the container the tenancy registration populates. Phase 40.11: ai-db holds the
/// dialog library, which becomes organization-authorable, so the context carries both tenancy
/// interceptors — the write guard, and the one that issues <c>SET LOCAL app.organization_id</c> for the
/// row-level-security policies.
/// </para>
///
/// <para>
/// <b>Never switch this to EF Core's pooled-context helper.</b> A pooled context reuses everything it
/// closed over at construction time, including the <c>ITenantContext</c>-backed global query filter,
/// across unrelated requests for different organizations — the first tenant to touch a pooled instance
/// would leak its filter onto every later caller. See <c>docs/CODESTYLE.md</c> §6 and
/// <c>scripts/tenancy-pool-lint.py</c>.
/// </para>
/// </summary>
public static class AiDataAccessServiceCollectionExtensions
{
    public static IServiceCollection AddAiDataAccess(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSellevateTenancy();

        services.AddDbContext<AiDbContext>((serviceProvider, databaseOptions) =>
            databaseOptions
                .UseNpgsql(configuration.GetConnectionString("Postgres"))
                .AddInterceptors(
                    serviceProvider.GetRequiredService<TenantSaveChangesInterceptor>(),
                    serviceProvider.GetRequiredService<TenantConnectionInterceptor>()));

        services.AddSingleton<IMongoClient>(_ =>
            new MongoClient(configuration.GetConnectionString("Mongo")));
        services.AddSingleton<MongoDbContext>();

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(configuration.GetConnectionString("Redis")!));

        return services;
    }
}
