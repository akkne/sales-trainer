using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sellevate.BuildingBlocks.Idempotency;
using Sellevate.BuildingBlocks.Messaging;
using Sellevate.BuildingBlocks.Outbox;

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
    /// </summary>
    public static IServiceCollection AddSellevateEventing(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<KafkaSettings>(configuration.GetSection(KafkaSettings.SectionName));
        services.Configure<ConsumerResilienceSettings>(configuration.GetSection(ConsumerResilienceSettings.SectionName));
        services.Configure<OutboxSettings>(configuration.GetSection(OutboxSettings.SectionName));
        services.AddSingleton<KafkaEventPublisher>();
        services.AddSingleton<IEventPublisher>(serviceProvider => serviceProvider.GetRequiredService<KafkaEventPublisher>());
        services.AddSingleton<IDeadLetterPublisher>(serviceProvider => serviceProvider.GetRequiredService<KafkaEventPublisher>());
        services.AddSingleton<IOutboxEventForwarder>(serviceProvider => serviceProvider.GetRequiredService<KafkaEventPublisher>());
        services.AddSingleton<IIdempotencyStore, RedisIdempotencyStore>();
        return services;
    }
}
