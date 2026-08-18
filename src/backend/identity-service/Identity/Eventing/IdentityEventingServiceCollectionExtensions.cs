using Sellevate.BuildingBlocks.DependencyInjection;
using Sellevate.BuildingBlocks.Outbox;
using Sellevate.Identity.Common.Constants;
using StackExchange.Redis;

namespace Sellevate.Identity.Eventing;

/// <summary>
/// Registers identity-service's outbound publishing, its outbox relay, and the one topic it consumes.
///
/// <para>
/// The Redis multiplexer is required by <c>AddSellevateEventing</c>'s idempotency store. Without it
/// the container fails validation at <c>builder.Build()</c> and the service crashes on startup, so the
/// two registrations belong together rather than in separate call sites that can drift apart.
/// </para>
///
/// <para>
/// Phase 40.9: identity-service becomes a consumer for the first time. It needs its own projection of
/// the tenant registry because it is the service that mints tokens, and a suspended organization has
/// to stop producing them (docs/TENANCY/TENANCY.md §1.1).
/// </para>
/// </summary>
public static class IdentityEventingServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityEventing(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(
                configuration.GetConnectionString(ConfigurationKeys.RedisConnectionName)!));

        services.AddSellevateEventing(configuration);
        services.AddScoped<IUserEventPublisher, KafkaUserEventPublisher>();
        services.AddScoped<IOutboxWriter, IdentityOutboxWriter>();
        services.AddScoped<IOutboxStore, IdentityOutboxStore>();
        services.AddHostedService<OutboxRelayBackgroundService>();
        services.AddHostedService<OrganizationReplicaConsumer>();

        return services;
    }
}
