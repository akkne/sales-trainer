using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sellevate.BuildingBlocks.Idempotency;
using Sellevate.BuildingBlocks.Messaging;
using Sellevate.BuildingBlocks.Outbox;
using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.BuildingBlocks.DependencyInjection;

/// <summary>
/// Registration helpers so each service wires the shared platform building blocks
/// (Kafka publisher + idempotency store) with one call.
/// </summary>
public static class BuildingBlocksServiceCollectionExtensions
{
    /// <summary>
    /// Binds <see cref="KafkaSettings"/> (<c>Kafka</c>), <see cref="ConsumerResilienceSettings"/>
    /// (<c>Kafka:ConsumerResilience</c>) and <see cref="OutboxSettings"/> (<c>Outbox</c>) from
    /// configuration and registers the singleton <see cref="IEventPublisher"/> and
    /// <see cref="IIdempotencyStore"/>.
    ///
    /// <para>
    /// Requires the host to have already registered an
    /// <see cref="StackExchange.Redis.IConnectionMultiplexer"/> (used by the idempotency
    /// store). Kafka consumers are registered per service by adding the concrete
    /// <see cref="KafkaConsumerBackgroundService"/> subclasses as hosted services.
    /// </para>
    ///
    /// <para>
    /// Also registers the tenancy primitives (<see cref="AddSellevateTenancy"/>):
    /// <see cref="KafkaConsumerBackgroundService"/> resolves the scoped <see cref="TenantContext"/>
    /// per message to enforce the tenant-context rule from the envelope, and outbox writers
    /// resolve <see cref="ITenantContext"/> to stamp the current organization onto every
    /// enqueued event.
    /// </para>
    /// </summary>
    public static IServiceCollection AddSellevateEventing(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<KafkaSettings>(configuration.GetSection(KafkaSettings.SectionName));
        services.Configure<ConsumerResilienceSettings>(configuration.GetSection(ConsumerResilienceSettings.SectionName));
        services.Configure<OutboxSettings>(configuration.GetSection(OutboxSettings.SectionName));

        // Registered first so its StartAsync creates all topics before any consumer subscribes —
        // otherwise a broker with auto-create disabled fails the consume loop with
        // "Unknown topic or partition" and no events are ever delivered.
        services.AddHostedService<KafkaTopicProvisioner>();
        services.AddSingleton<KafkaEventPublisher>();
        services.AddSingleton<IEventPublisher>(serviceProvider => serviceProvider.GetRequiredService<KafkaEventPublisher>());
        services.AddSingleton<IDeadLetterPublisher>(serviceProvider => serviceProvider.GetRequiredService<KafkaEventPublisher>());
        services.AddSingleton<IOutboxEventForwarder>(serviceProvider => serviceProvider.GetRequiredService<KafkaEventPublisher>());
        services.AddSingleton<IIdempotencyStore, RedisIdempotencyStore>();
        services.AddSellevateTenancy();
        return services;
    }

    public static IServiceCollection AddSellevateTenancy(this IServiceCollection services)
    {
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(serviceProvider => serviceProvider.GetRequiredService<TenantContext>());
        services.AddScoped<TenantSaveChangesInterceptor>();
        return services;
    }
}
