using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Sellevate.Social.Infrastructure.Configuration;

namespace Sellevate.Social.Infrastructure.Mongo;

/// <summary>
/// Phase 40.13 reduced this to the database handle alone.
///
/// <para>
/// It used to expose <c>ChatConversations</c> as a property, which meant any service could get an
/// unscoped collection handle. Mongo has no row-level security, so the tenant filter for chat is
/// application-level and is only auditable while there is exactly one place to audit — see
/// <c>ChatConversationRepository</c>, which creates the collection handle itself and is the only
/// code allowed to. Same structural move ai-service made for dialog sessions in 40.11.
/// </para>
/// </summary>
public sealed class MongoDbContext
{
    public MongoDbContext(IMongoClient mongoClient, IOptions<MongoConfiguration> mongoConfiguration)
    {
        Database = mongoClient.GetDatabase(mongoConfiguration.Value.DatabaseName);
    }

    public IMongoDatabase Database { get; }
}
