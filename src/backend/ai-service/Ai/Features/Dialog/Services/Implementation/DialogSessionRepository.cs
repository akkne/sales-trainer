using MongoDB.Bson;
using MongoDB.Driver;
using Sellevate.Ai.Features.Dialog.Models;
using Sellevate.Ai.Features.Dialog.Services.Abstract;
using Sellevate.Ai.Infrastructure.Mongo;
using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Ai.Features.Dialog.Services.Implementation;

/// <summary>
/// Phase 40.11. The only code in ai-service that touches the <c>dialog_sessions</c> collection.
///
/// <para>
/// Two properties make that claim enforceable rather than aspirational. First, the collection
/// handle is created here and nowhere else — <c>MongoDbContext</c> no longer exposes it, so
/// reaching around this class means writing <c>GetCollection&lt;DialogSession&gt;</c> by hand,
/// which <c>DialogSessionRepositoryIsTheOnlyMongoSessionReaderTests</c> turns into a failing build.
/// Second, every filter starts from <see cref="TenantFilter"/>, and
/// <see cref="RequireOrganizationId"/> throws when the tenant is unset, so there is no code path
/// through this class that returns another organization's sessions and none that returns all of
/// them.
/// </para>
///
/// <para>
/// There is deliberately no system-mode bypass. Nothing in ai-service reads sessions outside a
/// request today — the two Kafka consumers touch Postgres only — so admitting one would be adding
/// the escape hatch this class exists to remove. A future background reader has to add an explicit,
/// separately reviewed method, and 40.14's background-job registry is where that argument belongs.
/// </para>
/// </summary>
internal sealed class DialogSessionRepository : IDialogSessionRepository
{
    /// <summary>
    /// The collection name lives here and only here; see the class remarks. Keep it a literal on
    /// this line — the tripwire test greps for it.
    /// </summary>
    private const string CollectionName = "dialog_sessions";

    private readonly IMongoCollection<DialogSession> _sessions;
    private readonly ITenantContext _tenantContext;

    public DialogSessionRepository(MongoDbContext mongoContext, ITenantContext tenantContext)
    {
        _sessions = mongoContext.Database.GetCollection<DialogSession>(CollectionName);
        _tenantContext = tenantContext;
    }

    public async Task InsertAsync(DialogSession session, CancellationToken cancellationToken = default)
    {
        var organizationId = RequireOrganizationId();

        if (session.OrganizationId != Guid.Empty && session.OrganizationId != organizationId)
        {
            throw new CrossTenantWriteException(nameof(DialogSession), organizationId);
        }

        session.OrganizationId = organizationId;
        await _sessions.InsertOneAsync(session, cancellationToken: cancellationToken);
    }

    public async Task<DialogSession?> FindForUserAsync(
        string sessionId,
        Guid userId,
        CancellationToken cancellationToken = default)
        => await _sessions.Find(SessionOfUserFilter(sessionId, userId)).FirstOrDefaultAsync(cancellationToken);

    public async Task<List<DialogSession>> ListForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<DialogSession>.Filter.And(
            TenantFilter(),
            Builders<DialogSession>.Filter.Eq(session => session.UserId, userId));
        var sort = Builders<DialogSession>.Sort.Descending(session => session.CreatedAt);

        return await _sessions.Find(filter).Sort(sort).ToListAsync(cancellationToken);
    }

    public async Task AppendMessagesAsync(
        string sessionId,
        Guid userId,
        IReadOnlyCollection<DialogMessage> messages,
        CancellationToken cancellationToken = default)
        => await _sessions.UpdateOneAsync(
            SessionOfUserFilter(sessionId, userId),
            Builders<DialogSession>.Update.PushEach(session => session.Messages, messages),
            cancellationToken: cancellationToken);

    public async Task AbandonAsync(
        string sessionId,
        Guid userId,
        CancellationToken cancellationToken = default)
        => await _sessions.UpdateOneAsync(
            SessionOfUserFilter(sessionId, userId),
            Builders<DialogSession>.Update
                .Set(session => session.Status, DialogSessionStatus.Abandoned)
                .Set(session => session.XpEarned, 0)
                .Set(session => session.CompletedAt, DateTime.UtcNow),
            cancellationToken: cancellationToken);

    public async Task CompleteAsync(
        string sessionId,
        Guid userId,
        DialogFeedback feedback,
        int experiencePointsEarned,
        CancellationToken cancellationToken = default)
        => await _sessions.UpdateOneAsync(
            SessionOfUserFilter(sessionId, userId),
            Builders<DialogSession>.Update
                .Set(session => session.Status, DialogSessionStatus.Completed)
                .Set(session => session.Feedback, feedback)
                .Set(session => session.XpEarned, experiencePointsEarned)
                .Set(session => session.CompletedAt, DateTime.UtcNow),
            cancellationToken: cancellationToken);

    public async Task<bool> DeleteForUserAsync(
        string sessionId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var result = await _sessions.DeleteOneAsync(SessionOfUserFilter(sessionId, userId), cancellationToken);
        return result.DeletedCount > 0;
    }

    public async Task<bool> IncrementVoiceSecondsAsync(
        string sessionId,
        Guid userId,
        int seconds,
        CancellationToken cancellationToken = default)
    {
        var result = await _sessions.UpdateOneAsync(
            SessionOfUserFilter(sessionId, userId),
            Builders<DialogSession>.Update.Inc(session => session.VoiceSeconds, seconds),
            cancellationToken: cancellationToken);

        return result.MatchedCount > 0;
    }

    public async Task<int> SumVoiceSecondsForUserAsync(
        Guid userId,
        DateTime since,
        CancellationToken cancellationToken = default)
    {
        var organizationId = RequireOrganizationId();

        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument
            {
                { "organizationId", organizationId.ToString() },
                { "userId", userId.ToString() },
                { "createdAt", new BsonDocument("$gte", since) },
                { "voiceSeconds", new BsonDocument("$gt", 0) },
            }),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", BsonNull.Value },
                { "total", new BsonDocument("$sum", "$voiceSeconds") },
            }),
        };

        using var cursor = await _sessions.AggregateAsync<BsonDocument>(pipeline, cancellationToken: cancellationToken);
        var document = await cursor.FirstOrDefaultAsync(cancellationToken);

        return document is null ? 0 : document["total"].ToInt32();
    }

    public async Task<List<DialogSessionVoiceUsage>> AggregateVoiceUsageAsync(
        DateTime dayStart,
        DateTime monthStart,
        CancellationToken cancellationToken = default)
    {
        var organizationId = RequireOrganizationId();

        // The organization is the first stage on purpose: with the compound index built by
        // docs/TENANCY/mongo/40.11_dialog_sessions_organization_backfill.js this narrows to one
        // tenant's slice before anything else runs, and no ordering of the later stages can widen
        // it again.
        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument
            {
                { "organizationId", organizationId.ToString() },
                { "voiceSeconds", new BsonDocument("$gt", 0) },
            }),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", "$userId" },
                { "total", new BsonDocument("$sum", "$voiceSeconds") },
                { "sessionCount", new BsonDocument("$sum", 1) },
                { "lastCallAt", new BsonDocument("$max", "$createdAt") },
                { "daily", new BsonDocument("$sum", new BsonDocument("$cond", new BsonArray
                    {
                        new BsonDocument("$gte", new BsonArray { "$createdAt", dayStart }),
                        "$voiceSeconds",
                        0,
                    })) },
                { "monthly", new BsonDocument("$sum", new BsonDocument("$cond", new BsonArray
                    {
                        new BsonDocument("$gte", new BsonArray { "$createdAt", monthStart }),
                        "$voiceSeconds",
                        0,
                    })) },
            }),
            new BsonDocument("$sort", new BsonDocument("monthly", -1)),
        };

        using var cursor = await _sessions.AggregateAsync<BsonDocument>(pipeline, cancellationToken: cancellationToken);
        var documents = await cursor.ToListAsync(cancellationToken);

        var usageEntries = new List<DialogSessionVoiceUsage>();
        foreach (var document in documents)
        {
            if (!Guid.TryParse(document["_id"].AsString, out var documentUserId))
            {
                continue;
            }

            usageEntries.Add(new DialogSessionVoiceUsage(
                documentUserId,
                document["total"].ToInt32(),
                document["sessionCount"].ToInt32(),
                document["lastCallAt"].ToUniversalTime(),
                document["daily"].ToInt32(),
                document["monthly"].ToInt32()));
        }

        return usageEntries;
    }

    private FilterDefinition<DialogSession> SessionOfUserFilter(string sessionId, Guid userId)
        => Builders<DialogSession>.Filter.And(
            TenantFilter(),
            Builders<DialogSession>.Filter.Eq(session => session.Id, sessionId),
            Builders<DialogSession>.Filter.Eq(session => session.UserId, userId));

    /// <summary>
    /// The one filter every read and write in this class starts from. Sessions are tenant data, not
    /// content: there is no "global session", so this is plain equality with no null branch.
    /// </summary>
    private FilterDefinition<DialogSession> TenantFilter()
        => Builders<DialogSession>.Filter.Eq(session => session.OrganizationId, RequireOrganizationId());

    /// <summary>
    /// Fails closed and loudly. Returning every session for an unset tenant would be the exact
    /// failure the roadmap names in 40.14; returning none would hide a misconfigured gateway behind
    /// an empty history screen, which is how a tenancy bug survives to production. The message
    /// matches <c>TenantSaveChangesInterceptor</c>'s word for word so operators grep once.
    /// </summary>
    private Guid RequireOrganizationId()
        => _tenantContext.OrganizationId
            ?? throw new InvalidOperationException("Organization context is not set.");
}
