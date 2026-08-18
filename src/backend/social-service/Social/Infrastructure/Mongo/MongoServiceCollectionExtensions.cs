using MongoDB.Driver;
using Sellevate.Social.Common.Constants;
using Sellevate.Social.Infrastructure.Configuration;

namespace Sellevate.Social.Infrastructure.Mongo;

/// <summary>
/// Registers the Mongo store chat conversations live in.
///
/// <para>
/// The client is a singleton because the driver owns its own connection pool and is documented to be
/// reused; the database handle wrapper is a singleton for the same reason. Neither holds tenant state —
/// the collection handle and every filter over it live in <c>ChatConversationRepository</c>, which is
/// scoped precisely because it does.
/// </para>
/// </summary>
public static class MongoServiceCollectionExtensions
{
    public static IServiceCollection AddSocialMongo(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MongoConfiguration>(
            configuration.GetSection(MongoConfiguration.SectionName));

        services.AddSingleton<IMongoClient>(_ =>
            new MongoClient(configuration.GetConnectionString(
                ConfigurationKeys.ConnectionStringNames.Mongo)));

        services.AddSingleton<MongoDbContext>();

        return services;
    }
}
