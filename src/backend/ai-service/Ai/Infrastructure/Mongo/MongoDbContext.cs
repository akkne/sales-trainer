using MongoDB.Driver;

namespace Sellevate.Ai.Infrastructure.Mongo;

/// <summary>
/// The Mongo database handle for ai-service.
///
/// <para>
/// Phase 40.11 deliberately removed the <c>DialogSessions</c> collection property that used to live
/// here. Mongo has no row-level security, so a collection anyone can reach is a tenant filter
/// anyone can forget; the collection is now obtained in exactly one place — the tenant-scoped
/// <c>DialogSessionRepository</c>, which cannot be constructed without an
/// <c>ITenantContext</c> and exposes no un-scoped read. Reaching past it means writing
/// <c>GetCollection&lt;DialogSession&gt;</c> yourself, which
/// <c>DialogSessionRepositoryIsTheOnlyMongoSessionReaderTests</c> fails the build for.
/// See docs/TENANCY/TENANCY.md 1.6 and docs/DECISIONS.md (2026-08-15, "Mongo sessions get a
/// repository, not a convention").
/// </para>
/// </summary>
public sealed class MongoDbContext
{
    public MongoDbContext(IMongoClient mongoClient, IConfiguration configuration)
    {
        var databaseName = configuration["Mongo:DatabaseName"] ?? "sallevate";
        Database = mongoClient.GetDatabase(databaseName);
    }

    public IMongoDatabase Database { get; }
}
