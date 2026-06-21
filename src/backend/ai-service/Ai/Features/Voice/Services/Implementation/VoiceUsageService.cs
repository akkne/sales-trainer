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
using StackExchange.Redis;

namespace Sellevate.Ai.Features.Voice.Services.Implementation;

internal sealed class VoiceUsageService : IVoiceUsageService
{
    private readonly MongoDbContext _mongoContext;
    private readonly AiDbContext _dbContext;
    private readonly IDatabase _redis;
    private readonly IOptions<VoiceFeatureConfiguration> _voiceFeatureOptions;
    private readonly ILogger<VoiceUsageService> _logger;

    // AI1: Lua script for atomic check-and-increment.
    // Returns the new counter value on success, or -1 if the limit would be exceeded.
    private const string ReserveLuaScript = @"
local key   = KEYS[1]
local limit = tonumber(ARGV[1])
local delta = tonumber(ARGV[2])
local ttl   = tonumber(ARGV[3])
local cur   = redis.call('GET', key)
local val   = cur and tonumber(cur) or 0
if limit > 0 and val + delta > limit then
    return -1
end
local newval = redis.call('INCRBY', key, delta)
if ttl > 0 then
    redis.call('EXPIREAT', key, ttl)
end
return newval";

    public VoiceUsageService(
        MongoDbContext mongoContext,
        AiDbContext dbContext,
        IConnectionMultiplexer connectionMultiplexer,
        IOptions<VoiceFeatureConfiguration> voiceFeatureOptions,
        ILogger<VoiceUsageService> logger)
    {
        _mongoContext = mongoContext;
        _dbContext = dbContext;
        _redis = connectionMultiplexer.GetDatabase();
        _voiceFeatureOptions = voiceFeatureOptions;
        _logger = logger;
    }

    public async Task<VoiceUsageDto> GetUsageAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var dayStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var dailyUsedSeconds = await SumSecondsAsync(userId, dayStart, cancellationToken);
        var monthlyUsedSeconds = await SumSecondsAsync(userId, monthStart, cancellationToken);

        var limits = _voiceFeatureOptions.Value;

        return new VoiceUsageDto
        {
            DailyUsedSeconds = dailyUsedSeconds,
            DailyLimitSeconds = limits.DailyLimitMinutes * 60,
            MonthlyUsedSeconds = monthlyUsedSeconds,
            MonthlyLimitSeconds = limits.MonthlyLimitMinutes * 60,
        };
    }

    /// <inheritdoc/>
    public async Task<int> ReserveSecondsAsync(Guid userId, int maxSeconds, CancellationToken cancellationToken = default)
    {
        var config = _voiceFeatureOptions.Value;
        var dailyLimit = config.DailyLimitMinutes * 60;
        var monthlyLimit = config.MonthlyLimitMinutes * 60;

        var now = DateTime.UtcNow;

        // Day window key expires at next UTC midnight.
        var dayEnd = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc).AddDays(1);
        var dayKey = RedisKey(userId, "day", now.Year, now.Month, now.Day);
        var dayTtlUnix = (long)(dayEnd - DateTime.UnixEpoch).TotalSeconds;

        // Month window key expires at start of next month.
        var monthEnd = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
        var monthKey = RedisKey(userId, "month", now.Year, now.Month);
        var monthTtlUnix = (long)(monthEnd - DateTime.UnixEpoch).TotalSeconds;

        // Atomic daily reservation via Lua.
        var dayResult = (long)await _redis.ScriptEvaluateAsync(
            ReserveLuaScript,
            new RedisKey[] { dayKey },
            new RedisValue[] { dailyLimit, maxSeconds, dayTtlUnix });

        if (dayResult < 0)
        {
            var rawDay = await _redis.StringGetAsync(dayKey);
            var usedDaily = rawDay.HasValue ? (int)rawDay : 0;
            _logger.LogInformation("Voice daily limit exceeded for user {UserId}: ~{Used}s / {Limit}s", userId, usedDaily, dailyLimit);
            throw new VoiceUsageLimitException("daily", usedDaily, dailyLimit);
        }

        // Atomic monthly reservation via Lua.
        var monthResult = (long)await _redis.ScriptEvaluateAsync(
            ReserveLuaScript,
            new RedisKey[] { monthKey },
            new RedisValue[] { monthlyLimit, maxSeconds, monthTtlUnix });

        if (monthResult < 0)
        {
            // Roll back the daily reservation already made.
            await _redis.StringDecrementAsync(dayKey, maxSeconds);
            var rawMonth = await _redis.StringGetAsync(monthKey);
            var usedMonthly = rawMonth.HasValue ? (int)rawMonth : 0;
            _logger.LogInformation("Voice monthly limit exceeded for user {UserId}: ~{Used}s / {Limit}s", userId, usedMonthly, monthlyLimit);
            throw new VoiceUsageLimitException("monthly", usedMonthly, monthlyLimit);
        }

        _logger.LogDebug("Reserved {Seconds}s for user {UserId} — day={Day}, month={Month}",
            maxSeconds, userId, dayResult, monthResult);

        return maxSeconds;
    }

    /// <inheritdoc/>
    public async Task RefundReservationAsync(
        string sessionId,
        Guid userId,
        int reservedSeconds,
        int actualSeconds,
        CancellationToken cancellationToken = default)
    {
        // Clamp actual to reserved (can't exceed what was reserved).
        var billable = Math.Min(actualSeconds, reservedSeconds);
        var refund = reservedSeconds - billable;

        var now = DateTime.UtcNow;
        if (refund > 0)
        {
            // Return unused portion to Redis so concurrent streams have accurate headroom.
            var dayKey = RedisKey(userId, "day", now.Year, now.Month, now.Day);
            var monthKey = RedisKey(userId, "month", now.Year, now.Month);

            await Task.WhenAll(
                _redis.StringDecrementAsync(dayKey, refund),
                _redis.StringDecrementAsync(monthKey, refund));

            _logger.LogDebug("Refunded {Refund}s for user {UserId} (reserved={Reserved}, actual={Actual})",
                refund, userId, reservedSeconds, actualSeconds);
        }

        // Durable Mongo accounting for what was actually used.
        if (billable > 0)
            await RecordSessionSecondsAsync(sessionId, userId, billable, cancellationToken);
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

        var limits = _voiceFeatureOptions.Value;

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

    private static string RedisKey(Guid userId, string window, params int[] parts)
        => $"voice:{userId}:{window}:{string.Join(":", parts)}";
}
