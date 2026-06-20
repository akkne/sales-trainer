using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Sellevate.Ai.Features.Dialog.Models;
using Sellevate.Ai.Features.Voice.Models;
using Sellevate.Ai.Features.Voice.Services.Abstract;
using Sellevate.Ai.Infrastructure.Configuration;
using Sellevate.Ai.Infrastructure.Data;
using Sellevate.Ai.Infrastructure.Mongo;

namespace Sellevate.Ai.Features.Voice.Services.Implementation;

internal sealed class VoiceUsageService : IVoiceUsageService
{
    private readonly MongoDbContext _mongoContext;
    private readonly AiDbContext _dbContext;
    private readonly IOptions<VoiceUsageLimitsConfiguration> _voiceUsageLimitsOptions;
    private readonly ILogger<VoiceUsageService> _logger;

    public VoiceUsageService(
        MongoDbContext mongoContext,
        AiDbContext dbContext,
        IOptions<VoiceUsageLimitsConfiguration> voiceUsageLimitsOptions,
        ILogger<VoiceUsageService> logger)
    {
        _mongoContext = mongoContext;
        _dbContext = dbContext;
        _voiceUsageLimitsOptions = voiceUsageLimitsOptions;
        _logger = logger;
    }

    public async Task<VoiceUsageDto> GetUsageAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var dayStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var dailyUsedSeconds = await SumSecondsAsync(userId, dayStart, cancellationToken);
        var monthlyUsedSeconds = await SumSecondsAsync(userId, monthStart, cancellationToken);

        var limits = _voiceUsageLimitsOptions.Value;

        return new VoiceUsageDto
        {
            DailyUsedSeconds = dailyUsedSeconds,
            DailyLimitSeconds = limits.DailyLimitMinutes * 60,
            MonthlyUsedSeconds = monthlyUsedSeconds,
            MonthlyLimitSeconds = limits.MonthlyLimitMinutes * 60,
        };
    }

    public async Task EnsureWithinLimitsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var usage = await GetUsageAsync(userId, cancellationToken);
        if (usage.DailyExceeded)
            throw new VoiceUsageLimitException("daily", usage.DailyUsedSeconds, usage.DailyLimitSeconds);
        if (usage.MonthlyExceeded)
            throw new VoiceUsageLimitException("monthly", usage.MonthlyUsedSeconds, usage.MonthlyLimitSeconds);
    }

    public async Task RecordSessionSecondsAsync(string sessionId, Guid userId, int seconds, CancellationToken cancellationToken = default)
    {
        if (seconds <= 0) return;

        var sessionFilter = Builders<DialogSession>.Filter.And(
            Builders<DialogSession>.Filter.Eq(session => session.Id, sessionId),
            Builders<DialogSession>.Filter.Eq(session => session.UserId, userId)
        );
        var incrementUpdate = Builders<DialogSession>.Update.Inc(session => session.VoiceSeconds, seconds);
        var result = await _mongoContext.DialogSessions.UpdateOneAsync(sessionFilter, incrementUpdate, cancellationToken: cancellationToken);

        if (result.MatchedCount == 0)
        {
            _logger.LogWarning("Voice usage record skipped — session {SessionId} not found for user {UserId}", sessionId, userId);
        }
    }

    public async Task<AdminVoiceUsageDto> GetAllUsersUsageAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var dayStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var aggregationPipeline = new[]
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

        using var cursor = await _mongoContext.DialogSessions.AggregateAsync<BsonDocument>(aggregationPipeline, cancellationToken: cancellationToken);
        var documents = await cursor.ToListAsync(cancellationToken);

        var usageEntries = new List<AdminVoiceUsageEntryDto>();
        foreach (var document in documents)
        {
            if (!Guid.TryParse(document["_id"].AsString, out var documentUserId)) continue;
            usageEntries.Add(new AdminVoiceUsageEntryDto
            {
                UserId = documentUserId,
                TotalSeconds = document["total"].ToInt32(),
                SessionCount = document["sessionCount"].ToInt32(),
                LastCallAt = document["lastCallAt"].ToUniversalTime(),
                DailyUsedSeconds = document["daily"].ToInt32(),
                MonthlyUsedSeconds = document["monthly"].ToInt32(),
            });
        }

        var userIdentifiers = usageEntries.Select(entry => entry.UserId).ToList();
        var userProfiles = await _dbContext.UserReplicas
            .Where(user => userIdentifiers.Contains(user.UserId))
            .Select(user => new { user.UserId, user.Email, user.DisplayName })
            .ToDictionaryAsync(user => user.UserId, cancellationToken);

        foreach (var entry in usageEntries)
        {
            if (userProfiles.TryGetValue(entry.UserId, out var userProfile))
            {
                entry.Email = userProfile.Email;
                entry.DisplayName = userProfile.DisplayName;
            }
        }

        var limits = _voiceUsageLimitsOptions.Value;

        return new AdminVoiceUsageDto
        {
            DailyLimitSeconds = limits.DailyLimitMinutes * 60,
            MonthlyLimitSeconds = limits.MonthlyLimitMinutes * 60,
            Users = usageEntries,
        };
    }

    private async Task<int> SumSecondsAsync(Guid userId, DateTime since, CancellationToken cancellationToken)
    {
        var sumPipeline = new[]
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

        using var cursor = await _mongoContext.DialogSessions.AggregateAsync<BsonDocument>(sumPipeline, cancellationToken: cancellationToken);
        var document = await cursor.FirstOrDefaultAsync(cancellationToken);
        if (document == null) return 0;
        return document["total"].ToInt32();
    }
}
