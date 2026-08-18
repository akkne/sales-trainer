using Sellevate.BuildingBlocks.DependencyInjection;

namespace Sellevate.Social.Eventing;

/// <summary>
/// Registers both directions of Kafka traffic: the five events this service publishes, and the
/// consumer that keeps the local user directory in step with identity-service.
///
/// <para>
/// The publisher is scoped, not singleton, because it reads the request's <c>ITenantContext</c> to stamp
/// the organization onto every envelope — a singleton would capture the first request's organization and
/// mislabel every later event. The consumer is a hosted service and resolves a scope per message.
/// </para>
/// </summary>
public static class SocialEventingServiceCollectionExtensions
{
    public static IServiceCollection AddSocialEventing(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSellevateEventing(configuration);
        services.AddScoped<ISocialEventPublisher, KafkaSocialEventPublisher>();
        services.AddHostedService<UserReplicaConsumer>();

        return services;
    }
}
