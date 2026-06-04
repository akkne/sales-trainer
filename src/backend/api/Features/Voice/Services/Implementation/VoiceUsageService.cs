using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using MongoDB.Driver;
using SalesTrainer.Api.Features.Dialog.Models;
using SalesTrainer.Api.Features.Voice.Models;
using SalesTrainer.Api.Features.Voice.Services.Abstract;
using SalesTrainer.Api.Infrastructure.Data;
using SalesTrainer.Api.Infrastructure.Mongo;

namespace SalesTrainer.Api.Features.Voice.Services.Implementation;

public class VoiceUsageService : IVoiceUsageService
{
    private readonly MongoDbContext _mongo;
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<VoiceUsageService> _logger;

    public VoiceUsageService(MongoDbContext mongo, AppDbContext db, IConfiguration config, ILogger<VoiceUsageService> logger)
    {
        _mongo = mongo;
        _db = db;
        _config = config;
        _logger = logger;
    }

    public async Task<VoiceUsageDto> GetUsageAsync(Guid userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var dayStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var dailyUsed = await SumSecondsAsync(userId, dayStart, ct);
        var monthlyUsed = await SumSecondsAsync(userId, monthStart, ct);

        var dailyLimitMinutes = int.TryParse(_config["Voice:DailyLimitMinutes"], out var dl) ? dl : 0;
        var monthlyLimitMinutes = int.TryParse(_config["Voice:MonthlyLimitMinutes"], out var ml) ? ml : 0;

        return new VoiceUsageDto
        {
            DailyUsedSeconds = dailyUsed,
            DailyLimitSeconds = dailyLimitMinutes * 60,
            MonthlyUsedSeconds = monthlyUsed,
            MonthlyLimitSeconds = monthlyLimitMinutes * 60,
        };
    }

    public async Task EnsureWithinLimitsAsync(Guid userId, CancellationToken ct = default)
    {
        var usage = await GetUsageAsync(userId, ct);
        if (usage.DailyExceeded)
            throw new VoiceUsageLimitException("daily", usage.DailyUsedSeconds, usage.DailyLimitSeconds);
        if (usage.MonthlyExceeded)
            throw new VoiceUsageLimitException("monthly", usage.MonthlyUsedSeconds, usage.MonthlyLimitSeconds);
    }

    public async Task RecordSessionSecondsAsync(string sessionId, Guid userId, int seconds, CancellationToken ct = default)
    {
        if (seconds <= 0) return;

        var filter = Builders<DialogSession>.Filter.And(
            Builders<DialogSession>.Filter.Eq(s => s.Id, sessionId),
            Builders<DialogSession>.Filter.Eq(s => s.UserId, userId)
        );
        var update = Builders<DialogSession>.Update.Inc(s => s.VoiceSeconds, seconds);
        var result = await _mongo.DialogSessions.UpdateOneAsync(filter, update, cancellationToken: ct);

        if (result.MatchedCount == 0)
        {
            _logger.LogWarning("Voice usage record skipped — session {SessionId} not found for user {UserId}", sessionId, userId);
        }
    }

    public async Task<AdminVoiceUsageDto> GetAllUsersUsageAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var dayStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument
            {
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

        using var cursor = await _mongo.DialogSessions.AggregateAsync<BsonDocument>(pipeline, cancellationToken: ct);
        var docs = await cursor.ToListAsync(ct);

        var entries = new List<AdminVoiceUsageEntryDto>();
        foreach (var doc in docs)
        {
            if (!Guid.TryParse(doc["_id"].AsString, out var userId)) continue;
            entries.Add(new AdminVoiceUsageEntryDto
            {
                UserId = userId,
                TotalSeconds = doc["total"].ToInt32(),
                SessionCount = doc["sessionCount"].ToInt32(),
                LastCallAt = doc["lastCallAt"].ToUniversalTime(),
                DailyUsedSeconds = doc["daily"].ToInt32(),
                MonthlyUsedSeconds = doc["monthly"].ToInt32(),
            });
        }

        // Attach email/display name from PostgreSQL.
        var userIds = entries.Select(e => e.UserId).ToList();
        var users = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email, u.DisplayName })
            .ToDictionaryAsync(u => u.Id, ct);
        foreach (var entry in entries)
        {
            if (users.TryGetValue(entry.UserId, out var user))
            {
                entry.Email = user.Email;
                entry.DisplayName = user.DisplayName;
            }
        }

        var dailyLimitMinutes = int.TryParse(_config["Voice:DailyLimitMinutes"], out var dl2) ? dl2 : 0;
        var monthlyLimitMinutes = int.TryParse(_config["Voice:MonthlyLimitMinutes"], out var ml2) ? ml2 : 0;

        return new AdminVoiceUsageDto
        {
            DailyLimitSeconds = dailyLimitMinutes * 60,
            MonthlyLimitSeconds = monthlyLimitMinutes * 60,
            Users = entries,
        };
    }

    private async Task<int> SumSecondsAsync(Guid userId, DateTime since, CancellationToken ct)
    {
        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument
            {
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

        using var cursor = await _mongo.DialogSessions.AggregateAsync<BsonDocument>(pipeline, cancellationToken: ct);
        var doc = await cursor.FirstOrDefaultAsync(ct);
        if (doc == null) return 0;
        return doc["total"].ToInt32();
    }
}
