using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sellevate.Ai.Features.Dialog.Services.Abstract;
using Sellevate.Ai.Features.Quotas.Models;
using Sellevate.Ai.Features.Quotas.Services.Abstract;
using Sellevate.Ai.Features.Voice.Constants;
using Sellevate.Ai.Features.Voice.Models;
using Sellevate.Ai.Features.Voice.Services.Abstract;
using Sellevate.Ai.Infrastructure.Configuration;
using Sellevate.Ai.Infrastructure.Data;
using Sellevate.BuildingBlocks.Tenancy;
using StackExchange.Redis;

namespace Sellevate.Ai.Features.Voice.Services.Implementation;

/// <summary>
/// The voice reservation gate: a fast Redis counter that admits or refuses a turn, and the durable
/// Mongo record of what was actually spent.
///
/// <para>
/// <b>Two stores, on purpose.</b> Redis holds the live counters because a limit check has to be
/// atomic and cheap enough to sit in front of every turn; Mongo holds the truth, per session,
/// because Redis keys expire. Redis is therefore allowed to drift low — a lost key means a learner
/// gets extra headroom for one window — but never high, which is why every failure path returns its
/// reservation.
/// </para>
///
/// <para>
/// <b>Three gates in a fixed order, and rollback is manual.</b> The caller's day window, then their
/// month, then the organization's shared quota. Redis has no transaction spanning the Lua calls, so
/// each later refusal explicitly decrements what the earlier ones already took. Callers must not
/// reorder or add Redis round-trips here: the ordering is what makes a partial failure recoverable.
/// </para>
///
/// <para>
/// The per-user limits are not made redundant by the organization limit. They stop one person
/// burning a whole customer's day, which an organization-wide cap cannot express (Phase 40.33).
/// </para>
/// </summary>
internal sealed class VoiceUsageService : IVoiceUsageService
{
    private const int SecondsPerMinute = 60;

    private readonly IDialogSessionRepository _sessionRepository;
    private readonly AiDbContext _databaseContext;
    private readonly ITenantContext _tenantContext;
    private readonly IDatabase _redis;
    private readonly IOptions<VoiceFeatureConfiguration> _voiceFeatureOptions;
    private readonly IAiSpendMeter _spendMeter;
    private readonly IAiQuotaService _quotaService;
    private readonly ILogger<VoiceUsageService> _logger;

    /// <summary>
    /// Check-and-increment in one round-trip, because a read followed by a write would let two
    /// concurrent turns both see headroom that only one of them can have. Returns the new counter
    /// value, or <c>-1</c> when the increment would cross the limit — in which case nothing was
    /// written. A limit of 0 disables the window rather than closing it.
    /// </summary>
    private const string ReserveLuaScript = @"
local key            = KEYS[1]
local limit          = tonumber(ARGV[1])
local delta          = tonumber(ARGV[2])
local expiresAt      = tonumber(ARGV[3])
local storedReserved = redis.call('GET', key)
local reserved       = storedReserved and tonumber(storedReserved) or 0
if limit > 0 and reserved + delta > limit then
    return -1
end
local newReserved = redis.call('INCRBY', key, delta)
if expiresAt > 0 then
    redis.call('EXPIREAT', key, expiresAt)
end
return newReserved";

    public VoiceUsageService(
        IDialogSessionRepository sessionRepository,
        AiDbContext databaseContext,
        ITenantContext tenantContext,
        IConnectionMultiplexer connectionMultiplexer,
        IOptions<VoiceFeatureConfiguration> voiceFeatureOptions,
        IAiSpendMeter spendMeter,
        IAiQuotaService quotaService,
        ILogger<VoiceUsageService> logger)
    {
        _sessionRepository = sessionRepository;
        _databaseContext = databaseContext;
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
        var dayStart = StartOfDay(now);
        var monthStart = StartOfMonth(now);

        var dailyUsedSeconds = await _sessionRepository.SumVoiceSecondsForUserAsync(userId, dayStart, cancellationToken);
        var monthlyUsedSeconds = await _sessionRepository.SumVoiceSecondsForUserAsync(userId, monthStart, cancellationToken);

        var limits = _voiceFeatureOptions.Value;

        return new VoiceUsageDto
        {
            DailyUsedSeconds = dailyUsedSeconds,
            DailyLimitSeconds = ToSeconds(limits.DailyLimitMinutes),
            MonthlyUsedSeconds = monthlyUsedSeconds,
            MonthlyLimitSeconds = ToSeconds(limits.MonthlyLimitMinutes),
        };
    }

    /// <inheritdoc/>
    public async Task<int> ReserveSecondsAsync(Guid userId, int maxSeconds, CancellationToken cancellationToken = default)
    {
        var configuration = _voiceFeatureOptions.Value;
        var dailyLimit = ToSeconds(configuration.DailyLimitMinutes);
        var monthlyLimit = ToSeconds(configuration.MonthlyLimitMinutes);

        var now = DateTime.UtcNow;

        var dayKey = RedisKey(userId, VoiceUsageKeys.DayWindow, now.Year, now.Month, now.Day);
        var dayExpiresAtUnixSeconds = ToUnixSeconds(StartOfDay(now).AddDays(1));

        var monthKey = RedisKey(userId, VoiceUsageKeys.MonthWindow, now.Year, now.Month);
        var monthExpiresAtUnixSeconds = ToUnixSeconds(StartOfMonth(now).AddMonths(1));

        var dayResult = (long)await _redis.ScriptEvaluateAsync(
            ReserveLuaScript,
            new RedisKey[] { dayKey },
            new RedisValue[] { dailyLimit, maxSeconds, dayExpiresAtUnixSeconds });

        if (dayResult < 0)
        {
            var rawDay = await _redis.StringGetAsync(dayKey);
            var usedDaily = rawDay.HasValue ? (int)rawDay : 0;
            _logger.LogInformation("Voice daily limit exceeded for user {UserId}: ~{Used}s / {Limit}s", userId, usedDaily, dailyLimit);
            throw new VoiceUsageLimitException(VoiceUsagePeriods.Daily, usedDaily, dailyLimit);
        }

        var monthResult = (long)await _redis.ScriptEvaluateAsync(
            ReserveLuaScript,
            new RedisKey[] { monthKey },
            new RedisValue[] { monthlyLimit, maxSeconds, monthExpiresAtUnixSeconds });

        if (monthResult < 0)
        {
            await _redis.StringDecrementAsync(dayKey, maxSeconds);
            var rawMonth = await _redis.StringGetAsync(monthKey);
            var usedMonthly = rawMonth.HasValue ? (int)rawMonth : 0;
            _logger.LogInformation("Voice monthly limit exceeded for user {UserId}: ~{Used}s / {Limit}s", userId, usedMonthly, monthlyLimit);
            throw new VoiceUsageLimitException(VoiceUsagePeriods.Monthly, usedMonthly, monthlyLimit);
        }

        try
        {
            await _spendMeter.ReserveVoiceSecondsAsync(maxSeconds, cancellationToken);
        }
        catch (AiQuotaExceededException exception)
        {
            await _redis.StringDecrementAsync(dayKey, maxSeconds);
            await _redis.StringDecrementAsync(monthKey, maxSeconds);

            throw new VoiceUsageLimitException(
                $"{VoiceUsagePeriods.OrganizationPrefix} {exception.Period}", (int)exception.Used, (int)exception.Limit);
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
        var billableSeconds = Math.Min(actualSeconds, reservedSeconds);
        var refundSeconds = reservedSeconds - billableSeconds;

        var now = DateTime.UtcNow;
        if (refundSeconds > 0)
        {
            var dayKey = RedisKey(userId, VoiceUsageKeys.DayWindow, now.Year, now.Month, now.Day);
            var monthKey = RedisKey(userId, VoiceUsageKeys.MonthWindow, now.Year, now.Month);

            await Task.WhenAll(
                _redis.StringDecrementAsync(dayKey, refundSeconds),
                _redis.StringDecrementAsync(monthKey, refundSeconds));

            await _spendMeter.RefundVoiceSecondsAsync(refundSeconds, cancellationToken);

            _logger.LogDebug("Refunded {Refund}s for user {UserId} (reserved={Reserved}, actual={Actual})",
                refundSeconds, userId, reservedSeconds, actualSeconds);
        }

        if (billableSeconds > 0)
            await RecordSessionSecondsAsync(sessionId, userId, billableSeconds, cancellationToken);
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

    /// <summary>
    /// Every user's voice spend, scoped to the caller's organization by the repository (Phase 40.11).
    /// This screen once aggregated the whole installation; a cross-tenant total is the leak that block
    /// closed. Platform staff reach another customer's numbers by impersonating into it (40.9).
    /// </summary>
    public async Task<AdminVoiceUsageDto> GetAllUsersUsageAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var dayStart = StartOfDay(now);
        var monthStart = StartOfMonth(now);

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
        var userProfiles = await _databaseContext.UserReplicas
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
            DailyLimitSeconds = ToSeconds(limits.DailyLimitMinutes),
            MonthlyLimitSeconds = ToSeconds(limits.MonthlyLimitMinutes),
            OrganizationDailyLimitSeconds = ToSeconds(organizationQuota.VoiceDailyLimitMinutes),
            OrganizationMonthlyLimitSeconds = ToSeconds(organizationQuota.VoiceMonthlyLimitMinutes),
            OrganizationUsedSecondsToday = organizationDaySeconds,
            OrganizationUsedSecondsThisMonth = organizationMonthSeconds,
            Users = usageEntries,
        };
    }

    private static int ToSeconds(int minutes) => minutes * SecondsPerMinute;

    private static DateTime StartOfDay(DateTime moment)
        => new(moment.Year, moment.Month, moment.Day, 0, 0, 0, DateTimeKind.Utc);

    private static DateTime StartOfMonth(DateTime moment)
        => new(moment.Year, moment.Month, 1, 0, 0, 0, DateTimeKind.Utc);

    private static long ToUnixSeconds(DateTime utcMoment)
        => (long)(utcMoment - DateTime.UnixEpoch).TotalSeconds;

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

        var separator = VoiceUsageKeys.Separator;
        return string.Join(separator,
            VoiceUsageKeys.OrganizationPrefix,
            organizationId,
            VoiceUsageKeys.VoiceUsagePrefix,
            userId,
            window,
            string.Join(separator, parts));
    }
}
