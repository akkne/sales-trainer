using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sellevate.Ai.Features.Dialog.Services.Abstract;
using Sellevate.Ai.Features.Quotas.Models;
using Sellevate.Ai.Features.Quotas.Services.Abstract;
using Sellevate.Ai.Features.Voice.Models;
using Sellevate.Ai.Features.Voice.Services.Abstract;
using Sellevate.Ai.Infrastructure.Configuration;
using Sellevate.Ai.Infrastructure.Data;
using Sellevate.BuildingBlocks.Tenancy;
using StackExchange.Redis;

namespace Sellevate.Ai.Features.Voice.Services.Implementation;

internal sealed class VoiceUsageService : IVoiceUsageService
{
    private readonly IDialogSessionRepository _sessionRepository;
    private readonly AiDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IDatabase _redis;
    private readonly IOptions<VoiceFeatureConfiguration> _voiceFeatureOptions;
    private readonly IAiSpendMeter _spendMeter;
    private readonly IAiQuotaService _quotaService;
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
        IDialogSessionRepository sessionRepository,
        AiDbContext dbContext,
        ITenantContext tenantContext,
        IConnectionMultiplexer connectionMultiplexer,
        IOptions<VoiceFeatureConfiguration> voiceFeatureOptions,
        IAiSpendMeter spendMeter,
        IAiQuotaService quotaService,
        ILogger<VoiceUsageService> logger)
    {
        _sessionRepository = sessionRepository;
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _redis = connectionMultiplexer.GetDatabase();
        _voiceFeatureOptions = voiceFeatureOptions;
        _spendMeter = spendMeter;
        _quotaService = quotaService;
        _logger = logger;
    }

    public async Task<VoiceUsageDto> GetUsageAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var dayStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var dailyUsedSeconds = await _sessionRepository.SumVoiceSecondsForUserAsync(userId, dayStart, cancellationToken);
        var monthlyUsedSeconds = await _sessionRepository.SumVoiceSecondsForUserAsync(userId, monthStart, cancellationToken);

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

        // Phase 40.33. The organization-wide gate, layered under the per-user one above. Until this
        // block a customer's total voice spend was however many users they had times the per-user
        // allowance — which is precisely the roadmap's «один клиент, гоняющий голос сутками». The
        // per-user limits stay: they stop one person burning the whole organization's day, which the
        // organization limit alone cannot.
        try
        {
            await _spendMeter.ReserveVoiceSecondsAsync(maxSeconds, cancellationToken);
        }
        catch (AiQuotaExceededException exception)
        {
            // Roll back both per-user reservations; the caller sees the same 429 shape it always has,
            // with the period naming the organization window rather than the user's.
            await _redis.StringDecrementAsync(dayKey, maxSeconds);
            await _redis.StringDecrementAsync(monthKey, maxSeconds);

            throw new VoiceUsageLimitException(
                $"organization {exception.Period}", (int)exception.Used, (int)exception.Limit);
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

            await _spendMeter.RefundVoiceSecondsAsync(refund, cancellationToken);

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

        var recorded = await _sessionRepository.IncrementVoiceSecondsAsync(sessionId, userId, seconds, cancellationToken);

        if (!recorded)
        {
            _logger.LogWarning("Voice usage record skipped — session {SessionId} not found for user {UserId}", sessionId, userId);
        }
    }

    public async Task<AdminVoiceUsageDto> GetAllUsersUsageAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var dayStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // Phase 40.11: scoped to the caller's organization by the repository. This screen is
        // SuperAdmin-only and used to aggregate the whole installation; it now aggregates one
        // organization, because a cross-tenant total is exactly the leak this block closes. A
        // platform superadmin reaches another organization's numbers by impersonating into it
        // (40.9), and the org-scoped admin surface is 40.20.
        var usage = await _sessionRepository.AggregateVoiceUsageAsync(dayStart, monthStart, cancellationToken);

        var usageEntries = usage
            .Select(entry => new AdminVoiceUsageEntryDto
            {
                UserId = entry.UserId,
                TotalSeconds = entry.TotalSeconds,
                SessionCount = entry.SessionCount,
                LastCallAt = entry.LastCallAt,
                DailyUsedSeconds = entry.DailyUsedSeconds,
                MonthlyUsedSeconds = entry.MonthlyUsedSeconds,
            })
            .ToList();

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
        var organizationQuota = await _quotaService.ResolveAsync(cancellationToken);
        var (organizationDaySeconds, organizationMonthSeconds) = await _spendMeter.GetVoiceSecondsAsync(cancellationToken);

        return new AdminVoiceUsageDto
        {
            DailyLimitSeconds = limits.DailyLimitMinutes * 60,
            MonthlyLimitSeconds = limits.MonthlyLimitMinutes * 60,
            OrganizationDailyLimitSeconds = organizationQuota.VoiceDailyLimitMinutes * 60,
            OrganizationMonthlyLimitSeconds = organizationQuota.VoiceMonthlyLimitMinutes * 60,
            OrganizationUsedSecondsToday = organizationDaySeconds,
            OrganizationUsedSecondsThisMonth = organizationMonthSeconds,
            Users = usageEntries,
        };
    }

    /// <summary>
    /// Phase 40.11. Every ai-service Redis key is namespaced by organization. A voice quota is
    /// already per-user and a user id is globally unique, so this particular key could not have
    /// leaked one customer's usage into another's — but the prefix is what makes "no ai-service key
    /// is shared across organizations" checkable by reading key names instead of reasoning about
    /// each one, and voice limits become an organization setting later in Phase 40. Unset tenant
    /// throws rather than silently sharing one counter between customers.
    /// </summary>
    private string RedisKey(Guid userId, string window, params int[] parts)
    {
        var organizationId = _tenantContext.OrganizationId
            ?? throw new InvalidOperationException("Organization context is not set.");

        return $"org:{organizationId}:voice:{userId}:{window}:{string.Join(":", parts)}";
    }
}
