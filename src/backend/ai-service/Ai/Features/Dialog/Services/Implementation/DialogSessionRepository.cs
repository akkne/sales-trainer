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
/// Second, every filter starts from <see cref="TenantReadFilter"/> or <see cref="TenantWriteFilter"/>,
/// and <see cref="RequireOrganizationId"/> throws when the tenant is unset, so there is no code path
/// through this class that returns another organization's sessions and none that returns all of
/// them.
/// </para>
///
/// <para>
/// The read/write split is not stylistic (40.14): reads widen for validated platform staff, writes
/// widen nowhere. See <see cref="TenantWriteFilter"/>.
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

    /// <summary>
    /// Phase 40.25. Ceiling on the РОП's transcript list. A meeting is prepared from a handful of
    /// conversations, and an unbounded list of documents that each carry their whole message array
    /// is a response nobody reads and a query nobody notices growing.
    /// </summary>
    private const int MaximumAdminPageSize = 100;

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
        => await _sessions.Find(SessionOfUserForReadFilter(sessionId, userId)).FirstOrDefaultAsync(cancellationToken);

    public async Task<List<DialogSession>> ListForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<DialogSession>.Filter.And(
            TenantReadFilter(),
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
            SessionOfUserForWriteFilter(sessionId, userId),
            Builders<DialogSession>.Update.PushEach(session => session.Messages, messages),
            cancellationToken: cancellationToken);

    public async Task AbandonAsync(
        string sessionId,
        Guid userId,
        CancellationToken cancellationToken = default)
        => await _sessions.UpdateOneAsync(
            SessionOfUserForWriteFilter(sessionId, userId),
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
            SessionOfUserForWriteFilter(sessionId, userId),
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
        var result = await _sessions.DeleteOneAsync(SessionOfUserForWriteFilter(sessionId, userId), cancellationToken);
        return result.DeletedCount > 0;
    }

    public async Task<bool> IncrementVoiceSecondsAsync(
        string sessionId,
        Guid userId,
        int seconds,
        CancellationToken cancellationToken = default)
    {
        var result = await _sessions.UpdateOneAsync(
            SessionOfUserForWriteFilter(sessionId, userId),
            Builders<DialogSession>.Update.Inc(session => session.VoiceSeconds, seconds),
            cancellationToken: cancellationToken);

        return result.MatchedCount > 0;
    }

    public async Task<int> SumVoiceSecondsForUserAsync(
        Guid userId,
        DateTime since,
        CancellationToken cancellationToken = default)
    {
        var matchStage = new BsonDocument
        {
            { "userId", userId.ToString() },
            { "createdAt", new BsonDocument("$gte", since) },
            { "voiceSeconds", new BsonDocument("$gt", 0) },
        };
        AddTenantMatch(matchStage);

        var pipeline = new[]
        {
            new BsonDocument("$match", matchStage),
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
        // The organization is the first key of the first stage on purpose: with the compound index
        // built by docs/TENANCY/mongo/40.11_dialog_sessions_organization_backfill.js this narrows to
        // one tenant's slice before anything else runs, and no ordering of the later stages can
        // widen it again. Platform staff are the one caller for whom the key is absent, and this
        // endpoint (admin voice usage) is exactly the screen they need it absent for.
        var matchStage = new BsonDocument
        {
            { "voiceSeconds", new BsonDocument("$gt", 0) },
        };
        AddTenantMatch(matchStage);

        var pipeline = new[]
        {
            new BsonDocument("$match", matchStage),
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

    /// <summary>
    /// Phase 40.25. The РОП's list of graded conversations. The tenant filter comes first, as it
    /// does everywhere in this class, so no combination of the optional filters can widen the read.
    /// </summary>
    public async Task<List<DialogSession>> ListGradedForOrganizationAsync(
        Guid? userId,
        Guid? modeId,
        int? maximumScore,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var filters = new List<FilterDefinition<DialogSession>>
        {
            TenantReadFilter(),
            Builders<DialogSession>.Filter.Ne(session => session.Feedback, null),
        };

        if (userId is { } requestedUserId && requestedUserId != Guid.Empty)
        {
            filters.Add(Builders<DialogSession>.Filter.Eq(session => session.UserId, requestedUserId));
        }

        if (modeId is { } requestedModeId && requestedModeId != Guid.Empty)
        {
            filters.Add(Builders<DialogSession>.Filter.Eq(session => session.ModeId, requestedModeId));
        }

        if (maximumScore is { } score)
        {
            filters.Add(Builders<DialogSession>.Filter.Lte(session => session.Feedback!.Score, score));
        }

        var sort = Builders<DialogSession>.Sort.Descending(session => session.CreatedAt);

        return await _sessions
            .Find(Builders<DialogSession>.Filter.And(filters))
            .Sort(sort)
            .Limit(Math.Clamp(limit, 1, MaximumAdminPageSize))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Phase 40.25. One conversation of this organization, whoever held it. A read, so platform
    /// staff reach it under the same rule as every other read here.
    /// </summary>
    public async Task<DialogSession?> FindForOrganizationAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<DialogSession>.Filter.And(
            TenantReadFilter(),
            Builders<DialogSession>.Filter.Eq(session => session.Id, sessionId));

        return await _sessions.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// One session, for a read. Platform staff may reach across organizations here.
    /// </summary>
    private FilterDefinition<DialogSession> SessionOfUserForReadFilter(string sessionId, Guid userId)
        => SessionOfUser(TenantReadFilter(), sessionId, userId);

    /// <summary>
    /// One session, for an update or a delete. Never reaches across organizations — see
    /// <see cref="TenantWriteFilter"/>.
    /// </summary>
    private FilterDefinition<DialogSession> SessionOfUserForWriteFilter(string sessionId, Guid userId)
        => SessionOfUser(TenantWriteFilter(), sessionId, userId);

    private static FilterDefinition<DialogSession> SessionOfUser(
        FilterDefinition<DialogSession> tenantFilter, string sessionId, Guid userId)
        => Builders<DialogSession>.Filter.And(
            tenantFilter,
            Builders<DialogSession>.Filter.Eq(session => session.Id, sessionId),
            Builders<DialogSession>.Filter.Eq(session => session.UserId, userId));

    /// <summary>
    /// The filter every <b>read</b> in this class starts from — the Mongo counterpart of a policy's
    /// <c>USING</c> clause. Sessions are tenant data, not content: there is no "global session", so
    /// this is plain equality with no null branch.
    ///
    /// <para>
    /// Platform-wide mode (validated Sellevate staff, 2026-08-16) drops the organization instead —
    /// Mongo has no row-level security, so what Postgres expresses as an <c>OR</c> in a policy's
    /// <c>USING</c> clause has to be expressed here. It is still not a bypass of the unset-tenant
    /// rule: a request with neither an organization nor platform mode reaches
    /// <see cref="RequireOrganizationId"/> and throws exactly as before.
    /// </para>
    /// </summary>
    private FilterDefinition<DialogSession> TenantReadFilter()
        => _tenantContext.IsPlatformWide
            ? Builders<DialogSession>.Filter.Empty
            : Builders<DialogSession>.Filter.Eq(session => session.OrganizationId, RequireOrganizationId());

    /// <summary>
    /// The filter every <b>write</b> in this class starts from — the counterpart of a policy's
    /// <c>WITH CHECK</c> clause, and deliberately not the same method as
    /// <see cref="TenantReadFilter"/>.
    ///
    /// <para>
    /// <b>Reads widen for platform staff; writes never widen anywhere</b> (docs/TENANCY/TENANCY.md
    /// §1.6a, docs/DECISIONS.md 2026-08-16). Postgres gets that asymmetry for free, because
    /// <c>TenantRlsMigrationBuilderExtensions</c> puts the <c>app.platform_mode</c> branch in
    /// <c>USING</c> and pointedly not in <c>WITH CHECK</c>. Mongo has no policies, so until the
    /// 40.14 audit both halves shared one method and a validated administrator could mutate or
    /// delete a document in an organization they never named. Splitting the two is what puts the
    /// asymmetry into code instead of into a comment — and it is what stops the next method added to
    /// this class from inheriting the wrong half silently.
    /// </para>
    /// </summary>
    private FilterDefinition<DialogSession> TenantWriteFilter()
        => Builders<DialogSession>.Filter.Eq(session => session.OrganizationId, RequireOrganizationId());

    /// <summary>
    /// The aggregation-pipeline counterpart of <see cref="TenantFilter"/>. The organization stays
    /// the first key of the <c>$match</c> stage so the compound index built by
    /// docs/TENANCY/mongo/40.11_dialog_sessions_organization_backfill.js still leads; in
    /// platform-wide mode the key is absent and the pipeline reads every organization.
    /// </summary>
    private void AddTenantMatch(BsonDocument matchStage)
    {
        if (_tenantContext.IsPlatformWide)
        {
            return;
        }

        matchStage.InsertAt(0, new BsonElement("organizationId", RequireOrganizationId().ToString()));
    }

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
