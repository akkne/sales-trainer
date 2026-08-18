using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sellevate.Ai.Features.Quotas.Models;
using Sellevate.Ai.Features.Quotas.Services.Abstract;
using Sellevate.Ai.Infrastructure.Configuration;
using Sellevate.Ai.Infrastructure.Data;
using Sellevate.BuildingBlocks.Tenancy;
using StackExchange.Redis;

namespace Sellevate.Ai.Features.Quotas.Services.Implementation;

/// <summary>
/// Phase 40.33. The meter. Every LLM completion and every speech call in the product is gated and
/// charged here, which is the single sentence the whole block exists to make true — see
/// <c>scripts/ai-provider-lint.py</c>, which fails the build of that sentence by grepping for a
/// second door.
/// </summary>
internal sealed class AiSpendMeter : IAiSpendMeter
{
    /// <summary>
    /// The header an internal caller declares its workload class in. Absent means interactive, which
    /// is the class with the *larger* allowance — so a caller that forgets the header gets the
    /// permissive answer, and only the two learning-service sweeps that actually set it get held
    /// back at the reserve. Making the default the strict one would have every un-updated caller
    /// silently stop at 90%.
    /// </summary>
    public const string WorkloadHeaderName = "X-Ai-Workload";

    private const string BatchWorkloadHeaderValue = "batch";

    /// <summary>
    /// Atomic check-and-increment, byte-identical in behaviour to the one
    /// <c>VoiceUsageService</c> has used since the voice feature shipped: the decision is derived
    /// from the value the same call writes, never read first and written second.
    /// </summary>
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

    private readonly AiDbContext _databaseContext;
    private readonly ITenantContext _tenantContext;
    private readonly IAiQuotaService _quotaService;
    private readonly IDatabase _redis;
    private readonly IOptions<AiQuotaConfiguration> _quotaOptions;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AiSpendMeter> _logger;

    public AiSpendMeter(
        AiDbContext databaseContext,
        ITenantContext tenantContext,
        IAiQuotaService quotaService,
        IConnectionMultiplexer connectionMultiplexer,
        IOptions<AiQuotaConfiguration> quotaOptions,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AiSpendMeter> logger)
    {
        _databaseContext = databaseContext;
        _tenantContext = tenantContext;
        _quotaService = quotaService;
        _redis = connectionMultiplexer.GetDatabase();
        _quotaOptions = quotaOptions;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public AiWorkloadClass WorkloadClass
    {
        get
        {
            var declared = _httpContextAccessor.HttpContext?.Request.Headers[WorkloadHeaderName].ToString();
            return string.Equals(declared, BatchWorkloadHeaderValue, StringComparison.OrdinalIgnoreCase)
                ? AiWorkloadClass.Batch
                : AiWorkloadClass.Interactive;
        }
    }

    public async Task EnsureLlmAllowanceAsync(string operation, CancellationToken cancellationToken = default)
    {
        var organizationId = _tenantContext.OrganizationId;
        if (organizationId is null)
        {
            // Platform staff reading their own installation still pass through here; they have no
            // organization and no budget of their own, and refusing them would break the admin
            // screens. Everything else with no tenant is a caller that forgot the header.
            if (_tenantContext.IsPlatformWide || _tenantContext.IsSystem)
            {
                return;
            }

            throw new AiUnattributedCallException(operation);
        }

        var quota = await _quotaService.ResolveAsync(cancellationToken);
        if (quota.LlmMonthlyTokenLimit <= 0)
        {
            return;
        }

        var spentTokens = await SumMonthlyTokensAsync(cancellationToken);
        var workloadClass = WorkloadClass;
        var ceiling = CeilingFor(quota, workloadClass);

        if (spentTokens < ceiling)
        {
            return;
        }

        var period = workloadClass == AiWorkloadClass.Batch ? "month_batch_reserve" : "month";

        // Information, not Warning: an organization reaching a limit somebody set for it is the
        // feature working, exactly as 40.28 says of a sufficiency refusal. A run of these against
        // one customer is a commercial signal, not an incident.
        _logger.LogInformation(
            "LLM quota reached for organization {OrganizationId} on {Operation} ({Workload}): {Used} of {Ceiling} tokens",
            organizationId, operation, workloadClass, spentTokens, ceiling);

        throw new AiQuotaExceededException("llm_tokens", period, spentTokens, ceiling);
    }

    public async Task<bool> HasLlmAllowanceAsync(
        AiWorkloadClass workloadClass,
        CancellationToken cancellationToken = default)
    {
        if (_tenantContext.OrganizationId is null)
        {
            return true;
        }

        var quota = await _quotaService.ResolveAsync(cancellationToken);
        if (quota.LlmMonthlyTokenLimit <= 0)
        {
            return true;
        }

        var spentTokens = await SumMonthlyTokensAsync(cancellationToken);
        return spentTokens < CeilingFor(quota, workloadClass);
    }

    public Task RecordLlmUsageAsync(
        string model,
        int promptTokens,
        int completionTokens,
        bool wasEstimated,
        CancellationToken cancellationToken = default)
        => AddToLedgerAsync(
            AiUsageKinds.Llm,
            model,
            promptTokens,
            completionTokens,
            speechCharacters: 0,
            wasEstimated,
            cancellationToken);

    public Task RecordSpeechUsageAsync(
        string kind,
        string provider,
        int characterCount,
        CancellationToken cancellationToken = default)
        => AddToLedgerAsync(
            kind,
            provider,
            promptTokens: 0,
            completionTokens: 0,
            characterCount,
            wasEstimated: false,
            cancellationToken);

    public int EstimateTokens(string text) => EstimateTokensFromLength(text?.Length ?? 0);

    public int EstimateTokensFromLength(int characterCount)
    {
        if (characterCount <= 0)
        {
            return 0;
        }

        var charactersPerToken = Math.Max(1, _quotaOptions.Value.EstimatedCharactersPerToken);
        return (int)Math.Ceiling(characterCount / (double)charactersPerToken);
    }

    public async Task ReserveVoiceSecondsAsync(int seconds, CancellationToken cancellationToken = default)
    {
        if (seconds <= 0 || _tenantContext.OrganizationId is null)
        {
            return;
        }

        var quota = await _quotaService.ResolveAsync(cancellationToken);
        var now = DateTime.UtcNow;

        var dayKey = AiVoiceQuotaKeys.Day(_tenantContext.OrganizationId.Value, now);
        var monthKey = AiVoiceQuotaKeys.Month(_tenantContext.OrganizationId.Value, now);

        var dayLimitSeconds = quota.VoiceDailyLimitMinutes * 60;
        var dayResult = await ReserveAsync(dayKey, dayLimitSeconds, seconds, AiVoiceQuotaKeys.DayExpiryUnix(now));
        if (dayResult < 0)
        {
            _logger.LogInformation(
                "Organization voice day limit reached for {OrganizationId}: {Limit}s",
                _tenantContext.OrganizationId, dayLimitSeconds);

            throw new AiQuotaExceededException("voice_minutes", "day", await ReadAsync(dayKey), dayLimitSeconds);
        }

        var monthLimitSeconds = quota.VoiceMonthlyLimitMinutes * 60;
        var monthResult = await ReserveAsync(monthKey, monthLimitSeconds, seconds, AiVoiceQuotaKeys.MonthExpiryUnix(now));
        if (monthResult < 0)
        {
            await _redis.StringDecrementAsync(dayKey, seconds);

            _logger.LogInformation(
                "Organization voice month limit reached for {OrganizationId}: {Limit}s",
                _tenantContext.OrganizationId, monthLimitSeconds);

            throw new AiQuotaExceededException("voice_minutes", "month", await ReadAsync(monthKey), monthLimitSeconds);
        }
    }

    public async Task RefundVoiceSecondsAsync(int seconds, CancellationToken cancellationToken = default)
    {
        if (seconds <= 0 || _tenantContext.OrganizationId is null)
        {
            return;
        }

        try
        {
            var now = DateTime.UtcNow;
            var organizationId = _tenantContext.OrganizationId.Value;
            await Task.WhenAll(
                _redis.StringDecrementAsync(AiVoiceQuotaKeys.Day(organizationId, now), seconds),
                _redis.StringDecrementAsync(AiVoiceQuotaKeys.Month(organizationId, now), seconds));
        }
        catch (Exception exception)
        {
            // A lost refund costs the organization headroom it already paid for; a thrown refund
            // costs the caller their reply. The reservation window closes on its own either way.
            _logger.LogWarning(exception, "Voice quota refund of {Seconds}s failed", seconds);
        }
    }

    public async Task<(int DaySeconds, int MonthSeconds)> GetVoiceSecondsAsync(
        CancellationToken cancellationToken = default)
    {
        if (_tenantContext.OrganizationId is not { } organizationId)
        {
            return (0, 0);
        }

        var now = DateTime.UtcNow;
        return (
            (int)await ReadAsync(AiVoiceQuotaKeys.Day(organizationId, now)),
            (int)await ReadAsync(AiVoiceQuotaKeys.Month(organizationId, now)));
    }

    private static long CeilingFor(ResolvedAiQuota quota, AiWorkloadClass workloadClass)
    {
        if (workloadClass != AiWorkloadClass.Batch)
        {
            return quota.LlmMonthlyTokenLimit;
        }

        var reservePercent = Math.Clamp(quota.BatchReservePercent, 0, 90);
        return quota.LlmMonthlyTokenLimit - (quota.LlmMonthlyTokenLimit * reservePercent / 100);
    }

    private async Task<long> SumMonthlyTokensAsync(CancellationToken cancellationToken)
    {
        var periodKey = AiUsagePeriod.Current();

        await using var tenantScope = await AiTenantTransactionScope.BeginReadAsync(_databaseContext, cancellationToken);

        var totals = await _databaseContext.AiUsageRecords
            .Where(record => record.PeriodKey == periodKey && record.Kind == AiUsageKinds.Llm)
            .Select(record => record.PromptTokens + record.CompletionTokens)
            .ToListAsync(cancellationToken);

        return totals.Sum();
    }

    /// <summary>
    /// The charge. A single <c>INSERT … ON CONFLICT DO UPDATE SET x = x + excluded.x</c>, so two
    /// concurrent completions on one organization cannot lose each other's tokens — the same
    /// property 40.27 required of the pipeline claim, for the same reason: the alternative costs
    /// money rather than a retry.
    /// </summary>
    private async Task AddToLedgerAsync(
        string kind,
        string model,
        int promptTokens,
        int completionTokens,
        int speechCharacters,
        bool wasEstimated,
        CancellationToken cancellationToken)
    {
        var organizationId = _tenantContext.OrganizationId;
        if (organizationId is null)
        {
            return;
        }

        if (!_databaseContext.Database.IsRelational())
        {
            return;
        }

        try
        {
            await using var tenantScope = await AiTenantTransactionScope.BeginWriteAsync(_databaseContext, cancellationToken);

            await _databaseContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO "AiUsageRecords"
                     ("OrganizationId", "PeriodKey", "Model", "Kind", "PromptTokens", "CompletionTokens",
                      "CallCount", "EstimatedCallCount", "SpeechCharacters", "UpdatedAt")
                 VALUES ({organizationId.Value}, {AiUsagePeriod.Current()}, {Truncate(model)}, {kind},
                         {(long)promptTokens}, {(long)completionTokens}, {1L}, {(wasEstimated ? 1L : 0L)},
                         {(long)speechCharacters}, {DateTime.UtcNow})
                 ON CONFLICT ("OrganizationId", "PeriodKey", "Model") DO UPDATE SET
                     "PromptTokens"       = "AiUsageRecords"."PromptTokens"       + EXCLUDED."PromptTokens",
                     "CompletionTokens"   = "AiUsageRecords"."CompletionTokens"   + EXCLUDED."CompletionTokens",
                     "CallCount"          = "AiUsageRecords"."CallCount"          + EXCLUDED."CallCount",
                     "EstimatedCallCount" = "AiUsageRecords"."EstimatedCallCount" + EXCLUDED."EstimatedCallCount",
                     "SpeechCharacters"   = "AiUsageRecords"."SpeechCharacters"   + EXCLUDED."SpeechCharacters",
                     "UpdatedAt"          = EXCLUDED."UpdatedAt"
                 """,
                cancellationToken);

            await tenantScope.CommitAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            // The call has already been made and already been billed by the provider. Losing our own
            // record of it understates the month; throwing here would also lose the answer the
            // customer paid for, which is strictly worse.
            _logger.LogWarning(
                exception,
                "Failed to record {Kind} usage for organization {OrganizationId} on model {Model}",
                kind, organizationId, model);
        }
    }

    private static string Truncate(string model) =>
        string.IsNullOrWhiteSpace(model) ? "unknown"
        : model.Length > 128 ? model[..128]
        : model;

    private async Task<long> ReserveAsync(string key, int limit, int delta, long expiryUnix)
    {
        var result = await _redis.ScriptEvaluateAsync(
            ReserveLuaScript,
            [key],
            [limit, delta, expiryUnix]);

        return (long)result;
    }

    private async Task<long> ReadAsync(string key)
    {
        var raw = await _redis.StringGetAsync(key);
        return raw.HasValue ? (long)raw : 0;
    }

}

/// <summary>The UTC month bucket every usage row and every monthly limit is counted in.</summary>
public static class AiUsagePeriod
{
    public static string Current() => From(DateTime.UtcNow);

    public static string From(DateTime moment) => moment.ToString("yyyy-MM");
}
