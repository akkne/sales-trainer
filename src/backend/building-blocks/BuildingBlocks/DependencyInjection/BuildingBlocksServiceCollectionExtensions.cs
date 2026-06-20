using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sellevate.BuildingBlocks.Idempotency;
using Sellevate.BuildingBlocks.Messaging;

namespace Sellevate.BuildingBlocks.DependencyInjection;

/// <summary>
/// Registration helpers so each service wires the shared platform building blocks
/// (Kafka publisher + idempotency store) with one call.
/// </summary>
public static class BuildingBlocksServiceCollectionExtensions
{
    /// <summary>
    /// Binds <see cref="KafkaSettings"/> from the <c>Kafka</c> config section and registers
    /// the singleton <see cref="IEventPublisher"/> and <see cref="IIdempotencyStore"/>.
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
        services.AddSingleton<IEventPublisher, KafkaEventPublisher>();
        services.AddSingleton<IIdempotencyStore, RedisIdempotencyStore>();
        return services;
    }
}
